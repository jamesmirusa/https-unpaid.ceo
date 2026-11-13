using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>One chip: an optional caption key ("stat.energy", "npc.mom") and a signed value.</summary>
    public sealed class ChipView
    {
        public string CaptionKey;
        public string Value;
        public bool Good;
    }

    /// <summary>
    /// Effects as chips, from their definitions: cash, reserve, a stat, knowledge, an NPC's trust. Facts and
    /// demand modifiers are not shown (they are story, not numbers). Shared by places and by scenes.
    /// </summary>
    public static class EffectChips
    {
        public const string Minus = "\u2212";

        public static List<ChipView> Describe(IEnumerable<Effect> effects, string lang)
        {
            var chips = new List<ChipView>();
            foreach (var e in effects)
                switch (e)
                {
                    case CashEffect c: chips.Add(new ChipView { Value = SignedMoney(c.Amount, lang), Good = c.Amount.Cents >= 0 }); break;
                    case ReserveEffect r: chips.Add(new ChipView { CaptionKey = "hud.reserve", Value = SignedMoney(r.Amount, lang), Good = r.Amount.Cents >= 0 }); break;
                    case StatEffect s: chips.Add(new ChipView { CaptionKey = "stat." + s.Stat, Value = Signed(s.Delta), Good = s.Delta >= 0 }); break;
                    case StudyXpEffect x: chips.Add(new ChipView { CaptionKey = "stat.knowledge", Value = Signed(x.Amount), Good = x.Amount >= 0 }); break;
                    case NpcEffect n: chips.Add(new ChipView { CaptionKey = "npc." + n.Npc, Value = Signed(n.Delta), Good = n.Delta >= 0 }); break;
                }
            return chips;
        }

        /// <summary>Replaces the box's children with one chip per visible effect; returns how many.</summary>
        public static int Fill(VisualElement box, IEnumerable<Effect> effects, string lang)
        {
            box.Clear();
            var chips = Describe(effects, lang);
            foreach (var c in chips) box.Add(Chip(c.CaptionKey, c.Value, c.Good));
            return chips.Count;
        }

        public static string Signed(int n) => n < 0 ? Minus + (-n) : "+" + n;

        public static string SignedMoney(Money m, string lang) =>
            m.Cents < 0 ? Minus + MoneyFormat.Compact(Money.FromCents(-m.Cents), lang) : "+" + MoneyFormat.Compact(m, lang);

        public static VisualElement Chip(string captionKey, string value, bool good)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            if (captionKey != null) chip.Add(UiText.Label(captionKey, "chip__caption"));
            var v = UiText.ValueLabel(value, "chip__value");
            v.AddToClassList(good ? "value--positive" : "value--negative");
            chip.Add(v);
            return chip;
        }
    }
}
