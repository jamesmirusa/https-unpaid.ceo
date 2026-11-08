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
        public Dictionary<string, List<Effect>> Choices { get; set; } = new Dictionary<string, List<Effect>>();
        /// <summary>Choice id → Yarn line id of the option, so the journal can quote the chosen line in any language.</summary>
        public Dictionary<string, string> ChoiceLines { get; set; } = new Dictionary<string, string>();

        public static StoryletDef FromJson(JObject j) => j.ToObject<StoryletDef>();
    }
}
