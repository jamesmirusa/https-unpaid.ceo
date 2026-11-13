using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>A short scene: shown when all When conditions hold; each choice id maps to its effects (spec §3a).</summary>
    public sealed class StoryletDef
    {
        public string Id { get; set; }
        public string YarnNode { get; set; }
        public string Arc { get; set; }
        public List<Condition> When { get; set; } = new List<Condition>();
        public int Weight { get; set; } = 1;
        public bool Once { get; set; }
        public int CooldownMonths { get; set; }
        /// <summary>Always played when eligible, whatever the month's dice (chapter bosses).</summary>
        public bool Priority { get; set; }
        /// <summary>Stage 5B · the host the scene waits at (tony, mom…); null = a city event that comes by itself.</summary>
        public string Host { get; set; }
        /// <summary>Order in the host's line (lower first).</summary>
        public int Step { get; set; }
        /// <summary>A reaction to a past decision: it goes before the host's next step.</summary>
        public bool Reaction { get; set; }
        /// <summary>Choice id → tone of the answer: friendly | business | joke | firm (icon in a fixed place).</summary>
        public Dictionary<string, string> Tones { get; set; } = new Dictionary<string, string>();
        /// <summary>Choice id → knowledge the answer needs (shown locked with its requirement until then).</summary>
        public Dictionary<string, int> Requires { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, List<Effect>> Choices { get; set; } = new Dictionary<string, List<Effect>>();
        /// <summary>Choice id → Yarn line id of the option, so the journal can quote the chosen line in any language.</summary>
        public Dictionary<string, string> ChoiceLines { get; set; } = new Dictionary<string, string>();

        public static StoryletDef FromJson(JObject j) => j.ToObject<StoryletDef>();
    }
}
