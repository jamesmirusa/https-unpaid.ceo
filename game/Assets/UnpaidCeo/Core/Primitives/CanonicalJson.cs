using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnpaidCeo.Core.Primitives
{
    /// <summary>Deterministic JSON: object keys sorted ordinally (recursively), no whitespace, invariant culture.</summary>
    public static class CanonicalJson
    {
        static readonly JsonSerializer Serializer = JsonSerializer.Create(new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
        });

        public static string Serialize(object o)
        {
            var token = o == null ? JValue.CreateNull() : JToken.FromObject(o, Serializer);
            return Sort(token).ToString(Formatting.None);
        }

        static JToken Sort(JToken t)
        {
            switch (t)
            {
                case JObject obj:
                    var sorted = new JObject();
                    foreach (var p in obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                        sorted.Add(p.Name, Sort(p.Value));
                    return sorted;
                case JArray arr:
                    return new JArray(arr.Select(Sort));
                default:
                    return t;
            }
        }
    }
}
