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

        public bool IsNotFound => StatusCode == 404;
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

                string text = request.downloadHandler?.text;

                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        response.Body = JObject.Parse(text);
                    }
                    catch (JsonException)
                    {
                        // Non-JSON error page; keep the raw message below.
                    }
                }

                if (!response.Success)
                {
                    response.ErrorStatus = response.Body?["error"]?["status"]?.ToString()
                        ?? (request.result == UnityWebRequest.Result.ConnectionError ? "NETWORK_ERROR" : "UNKNOWN");
                    response.ErrorMessage = response.Body?["error"]?["message"]?.ToString() ?? request.error;

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
