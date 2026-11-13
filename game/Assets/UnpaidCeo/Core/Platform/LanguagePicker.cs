using System;
using System.Collections.Generic;
using System.Linq;

namespace UnpaidCeo.Core.Platform
{
    /// <summary>Picks the UI language: the player's saved choice, else the device/browser language, else English.</summary>
    public static class LanguagePicker
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["english"] = "en", ["russian"] = "ru",
        };

        public static string Pick(string saved, string device, IReadOnlyCollection<string> available)
        {
            if (!string.IsNullOrEmpty(saved) && available.Contains(saved)) return saved;
            var code = Normalize(device);
            if (code != null && available.Contains(code)) return code;
            return available.Contains("en") ? "en" : available.First();
        }

        /// <summary>"ru-RU", "ru_RU", "RU" or "Russian" (Unity SystemLanguage name) → "ru".</summary>
        public static string Normalize(string device)
        {
            if (string.IsNullOrWhiteSpace(device)) return null;
            var d = device.Trim();
            if (Names.TryGetValue(d, out var byName)) return byName;
            int cut = d.IndexOfAny(new[] { '-', '_' });
            return (cut > 0 ? d.Substring(0, cut) : d).ToLowerInvariant();
        }
    }
}
