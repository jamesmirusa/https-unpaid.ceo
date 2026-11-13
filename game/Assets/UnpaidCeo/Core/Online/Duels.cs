using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Online
{
    /// <summary>An async duel: both players run the same 6-month scenario (seed from the duel id) in the creator's industry.</summary>
    public static class Duels
    {
        public const int ExpiryDays = 7;

        public static ScenarioDef For(string duelId, string industryId) => new ScenarioDef
        {
            Seed = Hashes.Fnv1a64(duelId),
            IndustryId = industryId,
            StartCash = Money.FromCents(1_000_000),
            Months = 6,
        };
    }
}
