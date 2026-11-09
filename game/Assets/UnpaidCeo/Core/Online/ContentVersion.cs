using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// Fingerprint of the shipped content: SHA-256 of each file's canonical JSON (formatting and key order ignored), in
    /// GameBootstrap.ContentFiles order. A run only counts against the content the server embeds.
    /// </summary>
    public static class ContentVersion
    {
        public static string Of(IEnumerable<string> contentJson) =>
            StateHash.Compute(contentJson.Select(t => t == null ? (JToken)JValue.CreateNull() : JToken.Parse(t)).ToList());
    }
}
