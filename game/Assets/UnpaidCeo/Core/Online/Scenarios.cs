using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Online
{
    /// <summary>The scenario a challenge or duel run starts from; null for careers and for starts that name nothing valid.</summary>
    public static class Scenarios
    {
        public static ScenarioDef For(RunStart r, Balance b)
        {
            if (r == null) return null;
            if (r.Kind == RunStart.Challenge)
                return int.TryParse(r.ContextId, out var week) ? WeeklyChallenge.For(week, b) : null;
            if (r.Kind == RunStart.Duel)
                return !string.IsNullOrEmpty(r.ContextId) && r.IndustryId != null && b.Chapter2Industries.Contains(r.IndustryId)
                    ? Duels.For(r.ContextId, r.IndustryId) : null;
            return null;
        }
    }
}
