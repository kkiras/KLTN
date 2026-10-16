using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace KLTN.Infrastructure.Firestore
{
    /// <summary>
    /// Converts between plain C# values and Firestore REST typed values
    /// (for example 75 &lt;-&gt; {"integerValue":"75"}).
    /// </summary>
    public static class FirestoreValueMapper
    {
        public static JObject ToValue(object value)
        {
            switch (value)
            {
                case null:
                    return new JObject { ["nullValue"] = "NULL_VALUE" };
                case string text:
                    return new JObject { ["stringValue"] = text };
                case bool flag:
                    return new JObject { ["booleanValue"] = flag };
                case int number:
                    return new JObject { ["integerValue"] = number.ToString(CultureInfo.InvariantCulture) };
                case long number:
                    return new JObject { ["integerValue"] = number.ToString(CultureInfo.InvariantCulture) };
                case float number:
                    return new JObject { ["doubleValue"] = number };
                case double number:
                    return new JObject { ["doubleValue"] = number };
                case DateTime time:
                    return new JObject { ["timestampValue"] = time.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) };
                default:
                    throw new NotSupportedException($"Unsupported Firestore value type: {value.GetType().Name}");
            }
        }

        public static JObject ToFields(IReadOnlyDictionary<string, object> values)
        {
            var fields = new JObject();

            if (values == null)
            {
                return fields;
            }

            foreach (KeyValuePair<string, object> pair in values)
            {
                fields[pair.Key] = ToValue(pair.Value);
            }

            return fields;
        }

        public static long GetInteger(JObject document, string field, long fallback = 0)
        {
            JToken value = document?["fields"]?[field]?["integerValue"];

            return value != null && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)
                ? parsed
                : fallback;
        }

        /// <summary>Timestamp field as Unix milliseconds (UTC), or <paramref name="fallback"/>.</summary>
        public static long GetTimestampMs(JObject document, string field, long fallback = 0)
        {
            JToken value = document?["fields"]?[field]?["timestampValue"];

            if (value == null)
            {
                return fallback;
            }

            // Newtonsoft may already have converted the ISO string into a DateTime.
            if (value.Type == JTokenType.Date)
            {
                return new DateTimeOffset(value.Value<DateTime>().ToUniversalTime()).ToUnixTimeMilliseconds();
            }

            return DateTimeOffset.TryParse(
                value.ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed)
                ? parsed.ToUnixTimeMilliseconds()
                : fallback;
        }

        public static JObject TimestampFromMs(long unixMs)
        {
            return new JObject
            {
                ["timestampValue"] = DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
                    .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            };
        }

        public static string GetString(JObject document, string field)
        {
            return document?["fields"]?[field]?["stringValue"]?.ToString();
        }

        /// <summary>Last path segment of a document name, e.g. ".../ownedCards/MaCo" -> "MaCo".</summary>
        public static string DocumentId(JObject document)
        {
            string name = document?["name"]?.ToString();

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            int slash = name.LastIndexOf('/');
            return slash >= 0 ? name.Substring(slash + 1) : name;
        }
    }
}
