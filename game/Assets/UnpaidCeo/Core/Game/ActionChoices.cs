using System;
using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    /// <summary>Whether a free action can go into this week's plan, and if not, why.</summary>
    public enum ChoiceState
    {
        Open,
        /// <summary>Already in this week's plan: done, and closed until next week.</summary>
        Planned,
        /// <summary>Already taken in this turn (a plan that lists it twice): each free action at most once a turn.</summary>
        DoneThisTurn,
        /// <summary>It opens a fact (a debt, a scheme) that is still open.</summary>
        Waiting,
        /// <summary>The week's action points are spent.</summary>
        NoPoints,
        NoMoney,
        NoEnergy,
    }

    /// <summary>
    /// The choices at a place, New2NYC-style: what each costs in energy and money, whether the hero can still afford
    /// it this week, and which answers the hero's need. <see cref="GameEngine"/> settles a week with the same
    /// <see cref="Check"/>, so a choice the buttons offer is never dropped at the end of the week.
    /// </summary>
    public static class ActionChoices
    {
        /// <summary>Energy left above this is "low": burnout starts at the threshold, the nudge comes a little earlier.</summary>
        public const int LowEnergyMargin = 10;
        public const int LowRelationships = 40;
        public const int FewConnections = 15;

        /// <summary>The pool the coming week spends: every week brings its own (stage 5A).</summary>
        public static int PointsTotal(GameState s, ContentDb c) => LifeSimulator.WeeklyActionPoints(s.Life, c.Balance.Life);

        public static int PointsLeft(GameState s, ContentDb c, IEnumerable<string> planned) =>
            PointsTotal(s, c) - Defs(c, planned).Sum(fa => fa.ActionPoints);

        public static int EnergyDelta(FreeActionDef fa) =>
            fa.Effects.OfType<StatEffect>().Where(e => e.Stat == "energy").Sum(e => e.Delta);

        /// <summary>What an action takes from the hero's energy (0 for one that restores it).</summary>
        public static int EnergyCost(FreeActionDef fa) => Math.Max(0, -EnergyDelta(fa));

        /// <summary>The hero's energy once the planned actions are done, in the order the week settles them.</summary>
        public static int EnergyAfter(GameState s, ContentDb c, IEnumerable<string> planned)
        {
            int energy = s.Life.Energy;
            foreach (var fa in Ordered(c, new HashSet<string>(planned))) energy = Clamp(energy + EnergyDelta(fa));
            return energy;
        }

        /// <summary>
        /// The one rule for a free action at the moment the turn settles it: once a turn, not while its fact is open,
        /// within the points left, paid for, and never more energy than the hero has.
        /// </summary>
        public static ChoiceState Check(FreeActionDef fa, int pointsLeft, int energy, Money cash, ICollection<string> doneThisTurn, FactStore facts)
        {
            if (doneThisTurn.Contains(fa.Id)) return ChoiceState.DoneThisTurn;
            if (fa.Effects.OfType<SetFactEffect>().Any(f => facts.Has(f.FactId))) return ChoiceState.Waiting;
            if (fa.ActionPoints > pointsLeft) return ChoiceState.NoPoints;
            if (fa.Cost.Cents > 0 && cash < fa.Cost) return ChoiceState.NoMoney;
            if (energy + EnergyDelta(fa) < 0) return ChoiceState.NoEnergy;
            return ChoiceState.Open;
        }

        /// <summary>
        /// Plays the week's free actions with this one added: closed if it fails, or if it leaves a planned one
        /// without the energy, money or points that one needs.
        /// </summary>
        public static ChoiceState State(GameState s, ContentDb c, IEnumerable<string> planned, string id)
        {
            var set = new HashSet<string>(planned);
            if (set.Contains(id)) return ChoiceState.Planned;
            if (!c.FreeActions.ContainsKey(id)) return ChoiceState.Waiting;
            set.Add(id);
            int points = PointsTotal(s, c), energy = s.Life.Energy;
            var cash = s.Cash;
            var done = new HashSet<string>();   // every week starts fresh: only this plan counts
            ChoiceState? other = null;
            foreach (var fa in Ordered(c, set))
            {
                var st = Check(fa, points, energy, cash, done, s.Facts);
                if (st != ChoiceState.Open)
                {
                    if (fa.Id == id) return st;
                    other = other ?? st;
                    continue;
                }
                points -= fa.ActionPoints;
                energy = Clamp(energy + EnergyDelta(fa));
                cash -= fa.Cost;
                foreach (var e in fa.Effects.OfType<CashEffect>()) cash += e.Amount;
                done.Add(fa.Id);
            }
            return other ?? ChoiceState.Open;
        }

        /// <summary>
        /// A choice that answers what the hero lacks most: rest when tired (counting the energy this week's plan
        /// takes), family when they drift, people when no one knows you.
        /// </summary>
        public static bool Recommended(GameState s, ContentDb c, string id, IEnumerable<string> planned = null)
        {
            if (!c.FreeActions.TryGetValue(id, out var fa)) return false;
            int Gain(string stat) => fa.Effects.OfType<StatEffect>().Where(e => e.Stat == stat).Sum(e => e.Delta);
            var life = s.Life;
            int energy = planned != null ? EnergyAfter(s, c, planned) : life.Energy;
            if (energy < c.Balance.Life.BurnoutThreshold + LowEnergyMargin && Gain("energy") > 0) return true;
            if (life.Relationships < LowRelationships && Gain("relationships") > 0) return true;
            if (life.Connections < FewConnections && Gain("connections") > 0) return true;
            return false;
        }

        static IEnumerable<FreeActionDef> Defs(ContentDb c, IEnumerable<string> ids) =>
            ids.Distinct().Where(c.FreeActions.ContainsKey).Select(i => c.FreeActions[i]);

        /// <summary>The order the week settles them in: the plan lists them in content order.</summary>
        static IEnumerable<FreeActionDef> Ordered(ContentDb c, ISet<string> ids) => c.FreeActions.Values.Where(fa => ids.Contains(fa.Id));

        static int Clamp(int v) => v < 0 ? 0 : v > 100 ? 100 : v;
    }
}
