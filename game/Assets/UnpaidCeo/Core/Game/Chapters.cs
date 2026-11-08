using UnpaidCeo.Core.Content;

namespace UnpaidCeo.Core.Game
{
    public static class Chapters
    {
        /// <summary>The district a chapter is played in: 1 bronx, 2 brooklyn, 3 queens (later chapters stay in queens until stage 4).</summary>
        public static string District(int chapter) => chapter <= 1 ? "bronx" : chapter == 2 ? "brooklyn" : "queens";

        /// <summary>Chapter 1 (Hustler): cash ≥ goal and reserve ≥ one month of personal costs.</summary>
        public static bool Chapter1GoalMet(GameState s, Balance b) =>
            s.Cash >= b.Chapter1CashGoal && s.Reserve >= b.PersonalCosts.Total;
    }
}
