using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace KLTN.Infrastructure.Firestore
{
    public sealed class FirestoreResponse
    {
        public bool Success;
        public long StatusCode;

        /// <summary>Firestore error status, e.g. PERMISSION_DENIED, NOT_FOUND, ALREADY_EXISTS.</summary>
        public string ErrorStatus;
        public string ErrorMessage;
        public JObject Body;

        /// <summary>Server time from the HTTP Date header (1 s resolution), if present.</summary>
        public DateTimeOffset? ServerDate;

        public bool IsNotFound => StatusCode == 404;
    }

    /// <summary>
    /// Client estimate of Google's clock, learned from Firestore response Date headers.
    /// Used for season countdowns and the points lock so a wrong PC clock does not matter.
    /// Security Rules still decide with request.time.
    /// </summary>
    public static class ServerClock
    {
        private static long offsetMs;

        public static bool IsSynced { get; private set; }

        public static long UtcNowMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + offsetMs;

        public static void Observe(DateTimeOffset serverDate)
        {
            offsetMs = serverDate.ToUnixTimeMilliseconds() - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            IsSynced = true;
        }
    }

    /// <summary>
    /// Minimal Firestore REST client authenticated with the player's Firebase ID token.
    /// Uses the same UnityWebRequest pattern as FirebaseRestAuthService, so it works on
    /// Standalone, Editor and the Linux dedicated server without the Firestore SDK.
    /// </summary>
    public sealed class FirestoreRestClient
    {
        private const string FallbackProjectId = "unity-kltn";
        private const string ConfigFileName = "google-services-desktop.json";
        private const string Host = "https://firestore.googleapis.com/v1/";

        // runQuery / runAggregationQuery answer with a JSON array; it is wrapped under this key.
        private const string ArrayResultsKey = "results";

        public string ProjectId { get; }
        public string DatabaseRoot { get; }

        public FirestoreRestClient(string projectId = null, string databaseId = "(default)")
        {
            ProjectId = string.IsNullOrWhiteSpace(projectId) ? LoadProjectId() : projectId;
            DatabaseRoot = $"projects/{ProjectId}/databases/{databaseId}/documents";
        }

        /// <summary>Full resource name used in commit writes.</summary>
        public string DocumentName(string documentPath)
        {
            return $"{DatabaseRoot}/{documentPath.TrimStart('/')}";
        }

        #region Reads

        public Task<FirestoreResponse> GetDocumentAsync(string documentPath, string idToken)
        {
            return SendAsync(UnityWebRequest.kHttpVerbGET, Host + DocumentName(documentPath), null, idToken);
        }

        /// <summary>Lists every document in a (small) collection, following page tokens.</summary>
        public async Task<(FirestoreResponse response, List<JObject> documents)> ListDocumentsAsync(
            string collectionPath,
            string idToken
        )
        {
            var documents = new List<JObject>();
            string pageToken = null;

            do
            {
                string url = Host + DocumentName(collectionPath) + "?pageSize=300";

                if (!string.IsNullOrEmpty(pageToken))
                {
                    url += "&pageToken=" + UnityWebRequest.EscapeURL(pageToken);
                }

                FirestoreResponse response = await SendAsync(UnityWebRequest.kHttpVerbGET, url, null, idToken);

                if (!response.Success)
                {
                    return (response, documents);
                }

                if (response.Body?["documents"] is JArray page)
                {
                    foreach (JToken document in page)
                    {
                        documents.Add((JObject)document);
                    }
                }

                pageToken = response.Body?["nextPageToken"]?.ToString();
            }
            while (!string.IsNullOrEmpty(pageToken));

            return (new FirestoreResponse { Success = true, StatusCode = 200 }, documents);
        }

        /// <summary>
        /// Runs a structured query against a collection under <paramref name="parentPath"/>
        /// ("" for root collections). Returns the matched documents in query order.
        /// </summary>
        public async Task<(FirestoreResponse response, List<JObject> documents)> RunQueryAsync(
            string parentPath,
            JObject structuredQuery,
            string idToken
        )
        {
            var documents = new List<JObject>();
            var body = new JObject { ["structuredQuery"] = structuredQuery };
            FirestoreResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                Host + ParentName(parentPath) + ":runQuery",
                body.ToString(Formatting.None),
                idToken
            );

            if (response.Success && response.Body?[ArrayResultsKey] is JArray rows)
            {
                foreach (JToken row in rows)
                {
                    if (row["document"] is JObject document)
                    {
                        documents.Add(document);
                    }
                }
            }

            return (response, documents);
        }

        /// <summary>COUNT() aggregation of a structured query; costs ~1 read per 1000 matches.</summary>
        public async Task<(FirestoreResponse response, long count)> RunCountAsync(
            string parentPath,
            JObject structuredQuery,
            string idToken
        )
        {
            var body = new JObject
            {
                ["structuredAggregationQuery"] = new JObject
                {
                    ["structuredQuery"] = structuredQuery,
                    ["aggregations"] = new JArray
                    {
                        new JObject { ["alias"] = "total", ["count"] = new JObject() },
                    },
                },
            };

            FirestoreResponse response = await SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                Host + ParentName(parentPath) + ":runAggregationQuery",
                body.ToString(Formatting.None),
                idToken
            );

            long count = 0;

            if (response.Success && response.Body?[ArrayResultsKey] is JArray rows)
            {
                foreach (JToken row in rows)
                {
                    JToken value = row["result"]?["aggregateFields"]?["total"]?["integerValue"];

                    if (value != null && long.TryParse(value.ToString(), out long parsed))
                    {
                        count = parsed;
                    }
                }
            }

            return (response, count);
        }

        private string ParentName(string parentPath)
        {
            return string.IsNullOrEmpty(parentPath) ? DatabaseRoot : DocumentName(parentPath);
        }

        #endregion

        #region Writes

        /// <summary>Atomically applies all writes. Security Rules see the batch as one request.</summary>
        public Task<FirestoreResponse> CommitAsync(IReadOnlyList<JObject> writes, string idToken)
        {
            var body = new JObject { ["writes"] = new JArray(writes) };
            return SendAsync(
                UnityWebRequest.kHttpVerbPOST,
                Host + DatabaseRoot + ":commit",
                body.ToString(Formatting.None),
                idToken
            );
        }

        /// <summary>Create-only write; fails if the document already exists.</summary>
        public JObject CreateWrite(
            string documentPath,
            IReadOnlyDictionary<string, object> fields,
            params string[] serverTimestampFields
        )
        {
            var write = new JObject
            {
                ["update"] = new JObject
                {
                    ["name"] = DocumentName(documentPath),
                    ["fields"] = FirestoreValueMapper.ToFields(fields),
                },
                ["currentDocument"] = new JObject { ["exists"] = false },
            };

            AddTransforms(write, null, serverTimestampFields);
            return write;
        }

        /// <summary>
        /// Partial update of an existing document: sets <paramref name="fields"/>,
        /// atomically increments <paramref name="increments"/> and stamps server time.
        /// </summary>
        public JObject UpdateWrite(
            string documentPath,
            IReadOnlyDictionary<string, object> fields,
            IReadOnlyDictionary<string, long> increments,
            params string[] serverTimestampFields
        )
        {
            var mask = new JArray();

            if (fields != null)
            {
                foreach (string key in fields.Keys)
                {
                    mask.Add(key);
                }
            }

            var write = new JObject
            {
                ["update"] = new JObject
                {
                    ["name"] = DocumentName(documentPath),
                    ["fields"] = FirestoreValueMapper.ToFields(fields),
                },
                ["updateMask"] = new JObject { ["fieldPaths"] = mask },
                ["currentDocument"] = new JObject { ["exists"] = true },
            };

            AddTransforms(write, increments, serverTimestampFields);
            return write;
        }

        private static void AddTransforms(
            JObject write,
            IReadOnlyDictionary<string, long> increments,
            string[] serverTimestampFields
        )
        {
            var transforms = new JArray();

            if (increments != null)
            {
                foreach (KeyValuePair<string, long> pair in increments)
                {
                    if (pair.Value == 0)
                    {
                        continue;
                    }

                    transforms.Add(new JObject
                    {
                        ["fieldPath"] = pair.Key,
                        ["increment"] = FirestoreValueMapper.ToValue(pair.Value),
                    });
                }
            }

            if (serverTimestampFields != null)
            {
                foreach (string field in serverTimestampFields)
                {
                    transforms.Add(new JObject
                    {
                        ["fieldPath"] = field,
                        ["setToServerValue"] = "REQUEST_TIME",
                    });
                }
            }

            if (transforms.Count > 0)
            {
                write["updateTransforms"] = transforms;
            }
        }

        #endregion

        #region HTTP

        private static async Task<FirestoreResponse> SendAsync(string method, string url, string jsonBody, string idToken)
        {
            if (string.IsNullOrEmpty(idToken))
            {
                return new FirestoreResponse
                {
                    Success = false,
                    ErrorStatus = "UNAUTHENTICATED",
                    ErrorMessage = "Missing Firebase ID token.",
                };
            }

            using (var request = new UnityWebRequest(url, method))
            {
                if (jsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + idToken);
                request.timeout = 15;

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                var response = new FirestoreResponse
                {
                    StatusCode = request.responseCode,
                    Success = request.result == UnityWebRequest.Result.Success,
                };

                string date = request.GetResponseHeader("Date");

                if (!string.IsNullOrEmpty(date)
                    && DateTimeOffset.TryParse(
                        date,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset serverDate))
                {
                    response.ServerDate = serverDate;
                    ServerClock.Observe(serverDate);
                }

                string text = request.downloadHandler?.text;

                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        string trimmed = text.TrimStart();
                        response.Body = trimmed.StartsWith("[", StringComparison.Ordinal)
                            ? new JObject { [ArrayResultsKey] = JArray.Parse(trimmed) }
                            : JObject.Parse(text);
                    }
                    catch (JsonException)
                    {
                        // Non-JSON error page; keep the raw message below.
                    }
                }

                if (!response.Success)
                {
                    // runQuery reports errors inside its array: [{"error": {...}}].
                    JToken error = response.Body?["error"] ?? response.Body?[ArrayResultsKey]?.First?["error"];
                    response.ErrorStatus = error?["status"]?.ToString()
                        ?? (request.result == UnityWebRequest.Result.ConnectionError ? "NETWORK_ERROR" : "UNKNOWN");
                    response.ErrorMessage = error?["message"]?.ToString() ?? request.error;

                    if (!response.IsNotFound)
                    {
                        Debug.LogWarning($"[Firestore] {method} {url} -> {response.StatusCode} {response.ErrorStatus}: {response.ErrorMessage}");
                    }
                }

                return response;
            }
        }

        private static string LoadProjectId()
        {
            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);

                if (File.Exists(path))
                {
                    string projectId = JObject.Parse(File.ReadAllText(path))["project_info"]?["project_id"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        return projectId;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Firestore] Could not read project ID: {exception.Message}");
            }

            return FallbackProjectId;
        }

        #endregion
    }
}
