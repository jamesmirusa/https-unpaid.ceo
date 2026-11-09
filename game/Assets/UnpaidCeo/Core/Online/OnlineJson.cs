using System.Globalization;
using Newtonsoft.Json;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// The wire format between the game and its Cloud Code module: the save format's settings (invariant culture, lists
    /// replaced rather than appended), so a decision log reads back exactly as it was written on either side.
    /// </summary>
    public static class OnlineJson
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Formatting.None, Settings);

        /// <summary>The value, or default when the text is empty or not valid JSON for <typeparamref name="T"/>.</summary>
        public static T Deserialize<T>(string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonConvert.DeserializeObject<T>(json, Settings); }
            catch (JsonException) { return null; }
        }
    }
}
