using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Stage 5B · a scene's answers as a 2×2 grid, Mass-Effect style: each tone has a fixed place (friendly top-left,
    /// business top-right, joke bottom-left, firm bottom-right) and its icon; the price is on the button; an answer
    /// that needs knowledge is shown locked with its requirement, so studying pays off right in the conversation.
    /// </summary>
    public static class DialogueOptionsView
    {
        public static readonly string[] ToneOrder = { "friendly", "business", "joke", "firm" };

        public sealed class Option
        {
            public string Text;
            public string Tone;
            public bool Locked;
            public int RequiredKnowledge;
            public IReadOnlyList<Effect> Effects;
        }

        public static string ToneIcon(string tone) => tone switch
        {
            "friendly" => "tone_friendly", "business" => "tone_business", "joke" => "tone_joke", "firm" => "tone_firm", _ => "advice",
        };

        /// <summary>Fills <paramref name="box"/>; <paramref name="pick"/> gets the option's index in <paramref name="options"/>.</summary>
        public static void Build(VisualElement box, IList<Option> options, Action<int> pick, string lang)
        {
            box.Clear();
            box.AddToClassList("dialogue__options-grid");
            // answers take their tone's place; an answer without a tone, or a second one of a tone, follows in order
            var order = Enumerable.Range(0, options.Count)
                .OrderBy(i => { int t = Array.IndexOf(ToneOrder, options[i].Tone); return t < 0 ? 10 + i : t; })
                .ToList();
            foreach (int i in order)
            {
                var o = options[i];
                int index = i;
                // the words are a Label of their own: a Button with children does not size to its own text
                var b = new Button(() => pick(index)) { name = "option-" + i };
                b.AddToClassList("dialogue__option");
                var words = new Label(o.Text) { name = "option-text", pickingMode = PickingMode.Ignore };
                words.AddToClassList("dialogue__option-text");
                b.Add(words);
                if (o.Tone != null) b.AddToClassList("dialogue__option--" + o.Tone);
                b.Add(Icons.Game(ToneIcon(o.Tone), "dialogue__tone-icon"));
                if (o.Tone != null)
                {
                    var toneLabel = UiText.Label("tone." + o.Tone, "dialogue__tone");
                    toneLabel.pickingMode = PickingMode.Ignore;
                    b.Add(toneLabel);
                }
                if (o.Effects != null)
                {
                    var chips = new VisualElement { pickingMode = PickingMode.Ignore };
                    chips.AddToClassList("dialogue__option-chips");
                    foreach (var c in EffectChips.Describe(o.Effects.Where(e => e is Core.Narrative.CashEffect || (e is StatEffect s && s.Stat == "energy")), lang))
                        chips.Add(EffectChips.Chip(c.CaptionKey, c.Value, c.Good));
                    if (chips.childCount > 0) b.Add(chips);
                }
                if (o.Locked)
                {
                    b.SetEnabled(false);
                    b.AddToClassList("dialogue__option--locked");
                    var req = new VisualElement { name = "option-requires", pickingMode = PickingMode.Ignore };
                    req.AddToClassList("dialogue__requires");
                    req.Add(Icons.Service("lock-simple", "dialogue__lock"));
                    req.Add(UiText.Label("stat.knowledge", "dialogue__requires-text"));
                    req.Add(UiText.ValueLabel(o.RequiredKnowledge.ToString(), "dialogue__requires-text"));
                    b.Add(req);
                }
                box.Add(b);
            }
        }
    }
}
