using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Game
{
    public static class Chapters
    {
        /// <summary>Chapter 1 (Hustler): cash ≥ goal and reserve ≥ one month of personal costs.</summary>
        public static bool Chapter1GoalMet(GameState s, Balance b) =>
            s.Cash >= b.Chapter1CashGoal && s.Reserve >= b.PersonalCosts.Total;
    }
}
