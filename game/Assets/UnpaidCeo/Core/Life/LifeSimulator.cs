using System;

namespace UnpaidCeo.Core.Life
{
    /// <summary>Monthly effect of time allocation on the hero's life (spec §3a).</summary>
    public static class LifeSimulator
    {
        static int R(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        /// <summary>Applies the period's allocation to the stats and returns the change actually applied.</summary>
        /// <param name="share">The period as a share of a month: 1 for a month, 0.25 for a week.</param>
        public static LifeDelta Apply(LifeStats s, TimeAllocation t, LifeBalance b, double share = 1.0)
        {
            int energyDelta = R(share * (b.RestCoef * (t.Rest - b.RestBaseline) - b.BurnCoef * Math.Max(0, t.Work - b.WorkSafe)));
            int relDelta = R(share * b.FamilyCoef * (t.Family - b.FamilyBaseline));
            int xp = R(share * b.StudyCoef * t.Study);

            int e0 = s.Energy, r0 = s.Relationships;
            s.Energy += energyDelta;
            s.Relationships += relDelta;
            return new LifeDelta(s.Energy - e0, s.Relationships - r0, xp);
        }

        /// <summary>Base + skill bonus, minus one when burnt out; never below one.</summary>
        public static int ActionPoints(LifeStats s, int skillBonus, LifeBalance b)
        {
            int ap = b.BaseActionPoints + skillBonus - (s.Energy < b.BurnoutThreshold ? 1 : 0);
            return Math.Max(1, ap);
        }

        /// <summary>A week's points: the weekly base, minus one when burnt out; never below one.</summary>
        public static int WeeklyActionPoints(LifeStats s, LifeBalance b) =>
            Math.Max(1, b.BaseWeeklyActionPoints - (s.Energy < b.BurnoutThreshold ? 1 : 0));

        public static double ErrorChance(LifeStats s, LifeBalance b) =>
            s.Energy < b.BurnoutThreshold ? b.BurnoutErrorChance : 0.0;
    }
}
