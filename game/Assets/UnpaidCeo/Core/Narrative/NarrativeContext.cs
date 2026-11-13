using System.Collections.Generic;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>Read-only view of game state that conditions evaluate against.</summary>
    public sealed class NarrativeContext
    {
        public int Month { get; set; }
        public int Chapter { get; set; }
        public Money Cash { get; set; }
        public LifeStats Life { get; set; } = new LifeStats();
        public FactStore Facts { get; set; } = new FactStore();
        public NpcRelations Npcs { get; set; } = new NpcRelations();
        public IReadOnlyCollection<string> IndustryIds { get; set; } = new string[0];
    }
}
