using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// One choice as one big button (stage 5A): its title, what it costs and gives, and a status line that says why it is
    /// closed and when it opens. A tapped choice is done — it stays shown as done and closed until next week; there is
    /// no undo. Rooms and the Week screen build every choice with it, so they look and behave the same.
    /// </summary>
    public sealed class ChoiceCard : Button
    {
        public const string DoneClass = "visit__choice--planned";
        public const string RecommendedClass = "visit__choice--recommended";
        public const string ArmedClass = "visit__choice--armed";

        readonly VisualElement _chips;
        readonly Label _status;

        public Label StatusLabel => _status;

        /// <summary>A choice that cannot be undone and hurts (a loan shark, a gray scheme): the first tap asks, the second takes it.</summary>
        public bool Confirm { get; set; }
        /// <summary>Asked once and waiting for the second tap (any re-render forgets it).</summary>
        public bool Armed { get; private set; }

        public ChoiceCard(string name, string statusName, string titleKey, Action onTap)
        {
            clicked += () =>
            {
                if (Confirm && !Armed)
                {
                    Armed = true;
                    AddToClassList(ArmedClass);
                    UiText.Bind(_status, "choice.confirm");
                    _status.style.display = DisplayStyle.Flex;
                    return;
                }
                Armed = false;
                RemoveFromClassList(ArmedClass);
                onTap?.Invoke();
            };
            this.name = name;
            AddToClassList("visit__choice");
            Add(UiText.Label(titleKey, "visit__choice-label"));
            _chips = new VisualElement();
            _chips.AddToClassList("visit__choice-chips");
            _chips.style.display = DisplayStyle.None;
            Add(_chips);
            _status = UiText.Label("choice.recommended", "visit__choice-status");
            _status.name = statusName;
            _status.style.display = DisplayStyle.None;
            Add(_status);
        }

        /// <summary>What the choice costs and gives: money first, then each effect.</summary>
        public void SetChips(Money cost, IEnumerable<Effect> effects, string lang)
        {
            _chips.Clear();
            if (cost.Cents > 0) _chips.Add(EffectChips.Chip(null, EffectChips.Minus + MoneyFormat.Compact(cost, lang), false));
            foreach (var c in EffectChips.Describe(effects, lang)) _chips.Add(EffectChips.Chip(c.CaptionKey, c.Value, c.Good));
            _chips.style.display = _chips.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>Open cards can be tapped; done ones show "done", closed ones say why; a recommended open card says so.</summary>
        public void Show(ChoiceState state, bool recommended, int weeksUntilOpen = 0)
        {
            Armed = false;
            RemoveFromClassList(ArmedClass);
            bool done = state == ChoiceState.Planned || state == ChoiceState.DoneThisTurn;
            SetEnabled(state == ChoiceState.Open);
            EnableInClassList(DoneClass, done);
            EnableInClassList(RecommendedClass, recommended && state == ChoiceState.Open);
            string key = recommended && state == ChoiceState.Open ? "choice.recommended" : StatusKey(state, weeksUntilOpen);
            _status.style.display = key != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (key != null) UiText.Bind(_status, key);
        }

        /// <summary>The status line's key for a state (null for a plain open choice).</summary>
        public static string StatusKey(ChoiceState state, int weeksUntilOpen = 0)
        {
            switch (state)
            {
                case ChoiceState.Planned:
                case ChoiceState.DoneThisTurn: return weeksUntilOpen > 1 ? DoneKey(weeksUntilOpen) : "choice.done";
                case ChoiceState.Cooldown: return CooldownKey(weeksUntilOpen);
                case ChoiceState.Waiting: return "choice.waiting";
                case ChoiceState.NoPoints: return "choice.no_points";
                case ChoiceState.NoMoney: return "choice.no_money";
                case ChoiceState.NoEnergy: return "choice.no_energy";
                default: return null;
            }
        }

        /// <summary>"Again next week" … "again in 4 weeks": one key per count, so Russian gets its plural right.</summary>
        public static string CooldownKey(int weeks) => "choice.cooldown." + Math.Max(1, Math.Min(4, weeks));

        /// <summary>Done this week, and a monthly habit: "done — again in 4 weeks", not "again next week".</summary>
        public static string DoneKey(int weeks) => "choice.done.cooldown." + Math.Max(2, Math.Min(4, weeks));
    }
}
