using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Core.Online
{
    /// <summary>
    /// Achievement badges (spec §3.7), kept in MetaProgress (cosmetic, never hashed). "clean_cash_12": the spec's month
    /// without a cash gap holds in most single months, so the badge asks for twelve in a row after the first unit opens.
    /// </summary>
    public static class Achievements
    {
        public const string BreakEven3 = "break_even_3", CleanCash12 = "clean_cash_12", DuelWinner = "duel_winner", ChallengeTop10 = "challenge_top10";
        public static readonly string[] All = { BreakEven3, CleanCash12, DuelWinner, ChallengeTop10 };

        /// <summary>After a month: counts clean months and returns the badges earned now.</summary>
        public static IReadOnlyList<string> Update(MetaProgress m, GameState s, MonthReport r)
        {
            var earned = new List<string>();
            int? chosen = s.Facts.MonthSet("industry_chosen"), breakEven = s.Facts.MonthSet("break_even");
            if (chosen.HasValue && breakEven.HasValue && breakEven.Value - chosen.Value <= 3 && Grant(m, BreakEven3)) earned.Add(BreakEven3);

            if (s.Businesses.Any(b => b.Units > 0))
            {
                m.CleanCashMonths = s.Cash.Cents >= 0 ? m.CleanCashMonths + 1 : 0;
                if (m.CleanCashMonths >= 12 && Grant(m, CleanCash12)) earned.Add(CleanCash12);
            }
            return earned;
        }

        /// <summary>Adds a badge once; false when it was already there.</summary>
        public static bool Grant(MetaProgress m, string id)
        {
            if (m.Badges == null) m.Badges = new List<string>();
            if (m.Badges.Contains(id)) return false;
            m.Badges.Add(id);
            return true;
        }
    }
}
