using System;

namespace UnpaidCeo.Core.Life
{
    /// <summary>Monthly effect of time allocation on the hero's life (spec §3a).</summary>
    public static class LifeSimulator
    {
        static int R(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        /// <summary>Applies the month's allocation to the stats and returns the change actually applied.</summary>
        public static LifeDelta Apply(LifeStats s, TimeAllocation t, LifeBalance b)
        {
            int energyDelta = R(b.RestCoef * (t.Rest - b.RestBaseline) - b.BurnCoef * Math.Max(0, t.Work - b.WorkSafe));
            int relDelta = R(b.FamilyCoef * (t.Family - b.FamilyBaseline));
            int xp = R(b.StudyCoef * t.Study);

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

        public static double ErrorChance(LifeStats s, LifeBalance b) =>
            s.Energy < b.BurnoutThreshold ? b.BurnoutErrorChance : 0.0;
    }
}
