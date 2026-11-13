using System.Collections.Generic;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Narrative
{
    public static class EffectApplier
    {
        public static void Apply(Effect e, GameState s)
        {
            switch (e)
            {
                case CashEffect c: s.Cash += c.Amount; break;
                case StatEffect st: s.Life.Add(st.Stat, st.Delta); break;
                case NpcEffect n: s.Npcs.Add(n.Npc, n.Delta); break;
                case SetFactEffect f: s.Facts.Set(f.FactId, s.Month); break;
                case ClearFactEffect f: s.Facts.Clear(f.FactId); break;
                case ReserveEffect r:
                    // Only real money moves: never reserve debt, never withdraw more than is reserved.
                    long move = r.Amount.Cents >= 0
                        ? System.Math.Min(r.Amount.Cents, System.Math.Max(0, s.Cash.Cents))
                        : -System.Math.Min(-r.Amount.Cents, s.Reserve.Cents);
                    s.Cash -= Money.FromCents(move);
                    s.Reserve += Money.FromCents(move);
                    break;
                case LessonEffect l: s.PendingLessons.Add(l.LessonId); break;
                case StudyXpEffect x: s.StudyXp += x.Amount; break;
                case StaffEffect sf:
                    UnpaidCeo.Core.Economy.BusinessState target = null;
                    foreach (var b in s.Businesses)
                        if (b.Units > 0 && (target == null || b.Employees > target.Employees)) target = b;
                    if (target != null) target.Employees = System.Math.Max(0, target.Employees + sf.Delta);
                    break;
                case DemandModifierEffect d:
                    s.Modifiers.Add(new UnpaidCeo.Core.Economy.DemandModifier { Factor = d.Factor, MonthsLeft = d.Months, IndustryId = d.IndustryId });
                    break;
            }
        }

        public static void ApplyAll(IEnumerable<Effect> effects, GameState s)
        {
            foreach (var e in effects) Apply(e, s);
        }
    }
}
