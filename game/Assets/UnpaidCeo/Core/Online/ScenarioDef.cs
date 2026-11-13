using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Online
{
    /// <summary>A fixed start shared by every player of a challenge or duel: seed, industry, cash, length.</summary>
    public sealed class ScenarioDef
    {
        public ulong Seed { get; set; }
        public string IndustryId { get; set; }
        public Money StartCash { get; set; }
        public int Months { get; set; }
    }
}
