using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>One item of the radial menu: what it is, how it looks, whether it can be taken now, what it does.</summary>
    public sealed class RadialItem
    {
        public string Id;
        public string LabelKey;
        public string GameIcon;
        public string ServiceIcon;
        public ChoiceState State = ChoiceState.Open;
        public int WeeksUntilOpen;
        public bool Recommended;
        /// <summary>A step that cannot be undone (a loan shark): the first tap asks.</summary>
        public bool Confirm;
        /// <summary>Something waits here (a scene, "!"): it goes first and is marked.</summary>
        public bool Pending;
        /// <summary>The way in (talk, go in): first after anything waiting, not marked.</summary>
        public bool First;
        public Money Cost;
        public IReadOnlyList<Effect> Effects;
        /// <summary>A line for the chip when the item has no price (walk in, open the week).</summary>
        public string HintKey;
        public Action Run;
        public bool Open => State == ChoiceState.Open;
    }

    /// <summary>
    /// Stage 5B · "tap anything": a ring of up to six petals around what was tapped (icon + short label), a chip under
    /// it with the pressed item's price and effect or the reason it is closed, a dim backdrop that closes it. No nesting.
    /// Opens with a bounce (0.18 s). Irreversible items arm on the first tap and run on the second, like the cards.
    /// </summary>
    public sealed class RadialMenu
    {
        public const int MaxItems = 6;
        const float Radius = 250f, Petal = 190f, ChipHalf = 330f;

        readonly UiRoot _ui;
        readonly VisualElement _ring, _center, _chipBox, _chipEffects;
        readonly Label _chip, _title;
        readonly List<(RadialItem item, Button petal)> _petals = new List<(RadialItem, Button)>();
        Button _armed;

        public VisualElement Root { get; }
        public bool IsOpen => Root.style.display == DisplayStyle.Flex;
        public IReadOnlyList<RadialItem> Items => _petals.ConvertAll(p => p.item);

        public RadialMenu(UiRoot ui)
        {
            _ui = ui;
            Root = new VisualElement { name = "radial" };
            Root.AddToClassList("radial");
            var backdrop = new VisualElement { name = "radial-backdrop" };
            backdrop.AddToClassList("radial__backdrop");
            backdrop.RegisterCallback<ClickEvent>(_ => Close());
            Root.Add(backdrop);
            _ring = new VisualElement { name = "radial-ring", pickingMode = PickingMode.Ignore };
            _ring.AddToClassList("radial__ring");
            Root.Add(_ring);
            _center = new VisualElement { name = "radial-center", pickingMode = PickingMode.Ignore };
            _center.AddToClassList("radial__center");
            _title = new Label { name = "radial-title", pickingMode = PickingMode.Ignore };
            _title.AddToClassList("radial__title");
            _center.Add(_title);
            _ring.Add(_center);
            _chipBox = new VisualElement { name = "radial-chip-box", pickingMode = PickingMode.Ignore };
            _chipBox.AddToClassList("radial__chip-box");
            _chip = new Label { name = "radial-chip", pickingMode = PickingMode.Ignore };
            _chip.AddToClassList("radial__chip");
            _chipEffects = new VisualElement { name = "radial-chip-effects", pickingMode = PickingMode.Ignore };
            _chipEffects.AddToClassList("radial__chip-effects");
            _chipBox.Add(_chip);
            _chipBox.Add(_chipEffects);
            _ring.Add(_chipBox);
            // the press shows the item's chip at once — also on a closed petal, which gets no click
            Root.RegisterCallback<PointerDownEvent>(e => Select(Feel.ButtonOf(e.target as VisualElement)), TrickleDown.TrickleDown);
            Root.style.display = DisplayStyle.None;
        }

        /// <summary>Opens around <paramref name="localPoint"/> (in the app root's space) with the target's name and items.</summary>
        public void Show(string titleKey, IList<RadialItem> items, Vector2 localPoint)
        {
            foreach (var p in _petals) p.petal.RemoveFromHierarchy();
            _petals.Clear();
            _armed = null;
            if (titleKey != null) UiText.Bind(_title, titleKey); else _title.text = "";
            var list = Order(items);
            float w = _ui.Root.layout.width, h = _ui.Root.layout.height;
            if (float.IsNaN(w) || w <= 0) w = 1080;
            if (float.IsNaN(h) || h <= 0) h = 1920;
            float cx = Mathf.Clamp(localPoint.x, Radius + Petal / 2 + 8, w - Radius - Petal / 2 - 8);
            float cy = Mathf.Clamp(localPoint.y, Radius + Petal / 2 + 300, h - Radius - Petal / 2 - 420);
            _ring.style.left = cx;
            _ring.style.top = cy;
            // the chip under the ring stays on screen even when the ring sits near a side
            _chipBox.style.left = Mathf.Clamp(cx - ChipHalf, 16, w - 2 * ChipHalf - 16) - cx;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                float a = -Mathf.PI / 2 + i * 2 * Mathf.PI / list.Count;   // first item at the top, clockwise
                var b = new Button { name = "radial-" + item.Id };
                b.AddToClassList("radial__petal");
                b.style.left = Mathf.Cos(a) * Radius - Petal / 2;
                b.style.top = Mathf.Sin(a) * Radius - Petal / 2;
                var icon = item.GameIcon != null ? Icons.Game(item.GameIcon, "radial__icon") : Icons.Service(item.ServiceIcon ?? "sparkle", "radial__icon");
                b.Add(icon);
                b.Add(UiText.Label(item.LabelKey, "radial__label"));
                bool done = item.State == ChoiceState.Planned || item.State == ChoiceState.DoneThisTurn;
                b.EnableInClassList("radial__petal--done", done);
                b.EnableInClassList("radial__petal--pending", item.Pending);
                b.EnableInClassList("radial__petal--recommended", item.Recommended && item.Open);
                b.SetEnabled(item.Open);
                var it = item;
                b.clicked += () => Tap(it, b);
                _ring.Add(b);
                _petals.Add((item, b));
            }
            Select(_petals.Count > 0 ? _petals[0].petal : null);
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
            PopIn.Play(_ring);
            UnpaidCeo.Game.City.CameraRig.InputBlocked = true;
        }

        /// <summary>Waiting things first, then recommended open ones, then the rest in table order; at most six.</summary>
        public static List<RadialItem> Order(IList<RadialItem> items)
        {
            var list = new List<RadialItem>();
            foreach (var i in items) if (i.Pending) list.Add(i);
            foreach (var i in items) if (!i.Pending && i.First) list.Add(i);
            foreach (var i in items) if (!i.Pending && !i.First && i.Recommended && i.Open) list.Add(i);
            foreach (var i in items) if (!list.Contains(i)) list.Add(i);
            if (list.Count > MaxItems) list.RemoveRange(MaxItems, list.Count - MaxItems);
            return list;
        }

        void Tap(RadialItem item, Button petal)
        {
            if (!item.Open) return;
            if (item.Confirm && _armed != petal)
            {
                _armed = petal;
                petal.AddToClassList("radial__petal--armed");
                Select(petal);
                UiText.Bind(_chip, "choice.confirm");
                _chip.AddToClassList("radial__chip--closed");
                return;
            }
            Close();
            item.Run?.Invoke();
        }

        void Select(Button petal)
        {
            var found = _petals.Find(p => p.petal == petal);
            if (found.item == null) return;
            var item = found.item;
            if (_armed != null && _armed != petal) { _armed.RemoveFromClassList("radial__petal--armed"); _armed = null; }
            foreach (var p in _petals) p.petal.EnableInClassList("radial__petal--selected", p.petal == petal);
            _chipEffects.Clear();
            string key = ChoiceCard.StatusKey(item.State, item.WeeksUntilOpen);
            bool closed = !item.Open;
            if (item.Open && item.Effects != null)
            {
                if (item.Cost.Cents > 0) _chipEffects.Add(EffectChips.Chip(null, EffectChips.Minus + Core.Format.MoneyFormat.Compact(item.Cost, _ui.Language), false));
                foreach (var c in EffectChips.Describe(item.Effects, _ui.Language)) _chipEffects.Add(EffectChips.Chip(c.CaptionKey, c.Value, c.Good));
            }
            string line = closed ? key : item.Recommended ? "choice.recommended" : item.HintKey ?? item.LabelKey;
            UiText.Bind(_chip, line ?? item.LabelKey);
            _chip.EnableInClassList("radial__chip--closed", closed && !(item.State == ChoiceState.Planned || item.State == ChoiceState.DoneThisTurn));
            _chipEffects.style.display = _chipEffects.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Close()
        {
            if (!IsOpen) return;
            Root.style.display = DisplayStyle.None;
            _ui.UnblockCityInput();
        }
    }
}
