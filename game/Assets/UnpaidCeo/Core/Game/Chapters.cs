using System;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    public static class Chapters
    {
        /// <summary>The last chapter of this stage: chapter 4 arrives with stage 4.</summary>
        public const int LastChapter = 3;

        /// <summary>The district a chapter is played in: 1 bronx, 2 brooklyn, 3 queens (later chapters stay in queens until stage 4).</summary>
        public static string District(int chapter) => chapter <= 1 ? "bronx" : chapter == 2 ? "brooklyn" : "queens";

        /// <summary>Chapter 1 (Hustler): cash ≥ goal and reserve ≥ one month of personal costs.</summary>
        public static bool Chapter1GoalMet(GameState s, Balance b) =>
            s.Cash >= b.Chapter1CashGoal && s.Reserve >= b.PersonalCosts.Total;

        /// <summary>Cash + reserve + business units at resale value − what is still owed to banks.</summary>
        public static Money NetWorth(GameState s, ContentDb c)
        {
            var worth = s.Cash + s.Reserve;
            foreach (var bs in s.Businesses)
                if (bs.Units > 0 && c.Industries.TryGetValue(bs.IndustryId, out var def))
                    worth += Money.FromCents(def.Capex.MulRound(c.Balance.AssetResaleRate).Cents * bs.Units);
            foreach (var loan in s.Loans) worth -= loan.Balance;
            return worth;
        }

        /// <summary>Chapter 2 (Owner): a unit has broken even and net worth reached the capital goal.</summary>
        public static bool Chapter2GoalMet(GameState s, ContentDb c) =>
            s.Facts.Has("break_even") && NetWorth(s, c) >= c.Balance.Chapter2CapitalGoal;

        /// <summary>
        /// Chapter 3 (Boss): a second unit, and either a real team (kept for Chapter3TeamMonths month ends) or the capital goal.
        /// </summary>
        public static bool Chapter3GoalMet(GameState s, ContentDb c) =>
            MaxUnits(s) >= c.Balance.Chapter3UnitsGoal
            && (TeamKept(s, c.Balance) || NetWorth(s, c) >= c.Balance.Chapter3CapitalGoal);

        static bool TeamKept(GameState s, Balance b) =>
            s.Businesses.Sum(x => x.Employees) >= b.Chapter3EmployeesGoal && s.TeamMonths >= b.Chapter3TeamMonths;

        static double TeamPart(GameState s, Balance b)
        {
            double size = Clamp01(s.Businesses.Sum(x => x.Employees) / (double)Math.Max(1, b.Chapter3EmployeesGoal));
            return b.Chapter3TeamMonths <= 0 ? size : (size + Clamp01(s.TeamMonths / (double)b.Chapter3TeamMonths)) / 2;
        }

        public static bool GoalMet(GameState s, ContentDb c) =>
            s.Chapter <= 1 ? Chapter1GoalMet(s, c.Balance) : s.Chapter == 2 ? Chapter2GoalMet(s, c) : Chapter3GoalMet(s, c);

        /// <summary>How far the current chapter's goal is, 0..1, for the HUD bar: the mean of its parts.</summary>
        public static double Progress(GameState s, ContentDb c)
        {
            if (GoalMet(s, c)) return 1.0;
            var b = c.Balance;
            double p;
            if (s.Chapter <= 1)
                p = (Part(s.Cash, b.Chapter1CashGoal) + Part(s.Reserve, b.PersonalCosts.Total)) / 2;
            else if (s.Chapter == 2)
                p = ((s.Facts.Has("break_even") ? 1.0 : 0.0) + Part(NetWorth(s, c), b.Chapter2CapitalGoal)) / 2;
            else
                p = (Clamp01(MaxUnits(s) / (double)Math.Max(1, b.Chapter3UnitsGoal))
                     + Math.Max(TeamPart(s, b), Part(NetWorth(s, c), b.Chapter3CapitalGoal))) / 2;
            // An unmet goal never shows a full bar.
            return Math.Min(p, 0.99);
        }

        static int MaxUnits(GameState s) => s.Businesses.Count == 0 ? 0 : s.Businesses.Max(b => b.Units);

        static double Part(Money have, Money goal) => goal.Cents <= 0 ? 1.0 : Clamp01(have.Cents / (double)goal.Cents);

        static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;
    }
}
