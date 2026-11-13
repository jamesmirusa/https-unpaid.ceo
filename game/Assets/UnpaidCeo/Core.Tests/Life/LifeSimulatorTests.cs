using NUnit.Framework;
using UnpaidCeo.Core.Life;

namespace UnpaidCeo.Core.Tests.Life
{
    public class LifeSimulatorTests
    {
        static readonly LifeBalance B = new LifeBalance
        {
            RestBaseline = 25, RestCoef = 0.4, WorkSafe = 60, BurnCoef = 0.5,
            FamilyBaseline = 15, FamilyCoef = 0.5, StudyCoef = 0.2,
            BaseActionPoints = 3, BurnoutThreshold = 30, BurnoutErrorChance = 0.25,
        };

        static TimeAllocation T(int w, int f, int r, int s)
        {
            Assert.IsTrue(TimeAllocation.TryCreate(w, f, r, s, out var t));
            return t;
        }

        [Test]
        public void Rest40_Work70_nets_plus_one_energy()
        {
            var s = new LifeStats { Energy = 50, Relationships = 50 };
            // Work 70, Family 15, Rest 15 would not sum; use Work 45 + Rest 40 for the rest term and Work 70 for burn:
            var d1 = LifeSimulator.Apply(s, T(45, 15, 40, 0), B);   // +6 rest, no burn
            Assert.AreEqual(6, d1.Energy);
            var s2 = new LifeStats { Energy = 50 };
            var d2 = LifeSimulator.Apply(s2, T(70, 5, 25, 0), B);   // rest 0, burn −5
            Assert.AreEqual(-5, d2.Energy);
            Assert.AreEqual(45, s2.Energy);
        }

        [Test]
        public void Family5_costs_five_relationship_points()
        {
            var s = new LifeStats { Energy = 50, Relationships = 50 };
            var d = LifeSimulator.Apply(s, T(60, 5, 25, 10), B);
            Assert.AreEqual(-5, d.Relationships);
            Assert.AreEqual(45, s.Relationships);
        }

        [Test]
        public void Study_grants_xp()
        {
            var s = new LifeStats { Energy = 50 };
            Assert.AreEqual(4, LifeSimulator.Apply(s, T(45, 15, 20, 20), B).StudyXp);
        }

        [Test]
        public void Clamp_at_bounds()
        {
            var s = new LifeStats { Energy = 98 };
            s.Energy += 10;
            Assert.AreEqual(100, s.Energy);
            s.Energy = 2;
            s.Energy -= 10;
            Assert.AreEqual(0, s.Energy);
            var s2 = new LifeStats { Energy = 98 };
            var d = LifeSimulator.Apply(s2, T(25, 15, 50, 10), B);   // +10 computed, only +2 fits
            Assert.AreEqual(100, s2.Energy);
            Assert.AreEqual(2, d.Energy);
        }

        [Test]
        public void Burnout_costs_one_action_point_never_below_one()
        {
            Assert.AreEqual(3, LifeSimulator.ActionPoints(new LifeStats { Energy = 50 }, 0, B));
            Assert.AreEqual(2, LifeSimulator.ActionPoints(new LifeStats { Energy = 20 }, 0, B));
            Assert.AreEqual(3, LifeSimulator.ActionPoints(new LifeStats { Energy = 20 }, 1, B));
            var one = new LifeBalance { BaseActionPoints = 1, BurnoutThreshold = 30 };
            Assert.AreEqual(1, LifeSimulator.ActionPoints(new LifeStats { Energy = 0 }, 0, one));
            Assert.AreEqual(0.25, LifeSimulator.ErrorChance(new LifeStats { Energy = 20 }, B));
            Assert.AreEqual(0.0, LifeSimulator.ErrorChance(new LifeStats { Energy = 50 }, B));
        }

        [Test]
        public void TryCreate_rejects_sum_not_100_and_negatives()
        {
            Assert.IsFalse(TimeAllocation.TryCreate(50, 20, 20, 20, out _));
            Assert.IsFalse(TimeAllocation.TryCreate(-10, 50, 30, 30, out _));
            Assert.IsTrue(TimeAllocation.TryCreate(40, 20, 25, 15, out var t));
            Assert.AreEqual(40, t.Work);
        }
    }
}
