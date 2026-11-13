using System.Collections.Generic;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>World memory: fact id → month it was set. Canonical JSON sorts the keys for hashing.</summary>
    public sealed class FactStore
    {
        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();

        public void Set(string id, int month) => Items[id] = month;
        public void Clear(string id) => Items.Remove(id);
        public bool Has(string id) => Items.ContainsKey(id);
        public int? MonthSet(string id) => Items.TryGetValue(id, out var m) ? m : (int?)null;
    }

    /// <summary>NPC attitude toward the hero, clamped to −100..+100; unknown NPCs are 0.</summary>
    public sealed class NpcRelations
    {
        public Dictionary<string, int> Values { get; set; } = new Dictionary<string, int>();

        public int Get(string npc) => Values.TryGetValue(npc, out var v) ? v : 0;

        public void Add(string npc, int delta)
        {
            int v = Get(npc) + delta;
            Values[npc] = v > 100 ? 100 : v < -100 ? -100 : v;
        }
    }
}
