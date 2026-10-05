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
