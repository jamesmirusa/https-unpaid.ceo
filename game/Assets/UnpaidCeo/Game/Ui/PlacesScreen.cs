using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// The block and its places. A list of cards (one per place), and a place itself: its picture with the host, what
    /// the host says this month, how much they trust you, and what can be done there. A "plan" action is the same
    /// free action the month plan lists: tapping it here puts it into this month's plan, and it happens on "Next month".
    /// </summary>
    public sealed class PlacesScreen
    {
        const string Minus = "−";

        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly DecisionsScreen _plan;
        readonly VisualElement _list, _detail, _art, _actions;
        readonly Label _title, _host, _line, _trust, _points;
        readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        readonly Dictionary<string, VisualElement> _thumbs = new Dictionary<string, VisualElement>();
        static readonly Dictionary<string, Texture2D> Art = new Dictionary<string, Texture2D>();
        bool _thumbsRequested;

        /// <summary>The place on screen, or null while the list is.</summary>
        public string CurrentPlace { get; private set; }

        public PlacesScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state, DecisionsScreen plan)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            _plan = plan;

            _list = new VisualElement { name = "places-list" };
            _list.Add(UiText.Label("places.title", "screen__title"));
            var hint = UiText.Label("places.hint", "hint");
            hint.AddToClassList("places__hint");
            _list.Add(hint);
            foreach (var place in Places.All)
            {
                string id = place.Id;
                var card = new Button(() => Show(id)) { name = "place-card-" + id };
                card.AddToClassList("place-card");
                var thumb = new VisualElement();
                thumb.AddToClassList("place-card__thumb");
                _thumbs[id] = thumb;
                var text = new VisualElement();
                text.AddToClassList("place-card__text");
                text.Add(UiText.Label($"place.{id}.title", "place-card__title"));
                text.Add(UiText.Label($"place.{id}.host", "place-card__host"));
                card.Add(thumb);
                card.Add(text);
                _list.Add(card);
            }
            root.Add(_list);

            _detail = new VisualElement { name = "place-detail" };
            var back = UiText.Bind(new Button(ShowList) { name = "places-back" }, "places.back");
            back.AddToClassList("place__back");
            _detail.Add(back);
            _art = new VisualElement { name = "place-art" };
            _art.AddToClassList("place__art");
            _detail.Add(_art);
            _title = UiText.Label("places.title", "screen__title");
            _title.name = "place-title";
            _detail.Add(_title);
            _host = UiText.Label("places.title", "place__host");
            _host.name = "place-host";
            _detail.Add(_host);
            _line = UiText.Label("places.hint", "place__line");
            _line.name = "place-line";
            _detail.Add(_line);
            _actions = new VisualElement { name = "place-actions" };
            _detail.Add(_actions);
            _trust = Row(_detail, "place.trust", "place-trust");
            _points = Row(_detail, "decisions.ap", "place-points");
            _detail.Add(UiText.Label("place.hint", "hint"));
            root.Add(_detail);

            plan.PlanChanged += Refresh;
            ShowList();
        }

        static Label Row(VisualElement parent, string captionKey, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.Add(UiText.Label(captionKey, "row__label"));
            var value = UiText.ValueLabel("", "row__value");
            value.name = valueName;
            row.Add(value);
            parent.Add(row);
            return value;
        }

        public void ShowList()
        {
            CurrentPlace = null;
            _list.style.display = DisplayStyle.Flex;
            _detail.style.display = DisplayStyle.None;
        }

        /// <summary>The cards' pictures are fetched when the list is first looked at, not at boot.</summary>
        public void LoadThumbs()
        {
            if (_thumbsRequested) return;
            _thumbsRequested = true;
            foreach (var t in _thumbs) LoadArt(t.Key, t.Value, null);
        }

        public void Show(string id)
        {
            var place = Places.Find(id);
            if (place == null) throw new ArgumentException("unknown place " + id, nameof(id));
            CurrentPlace = id;
            _list.style.display = DisplayStyle.None;
            _detail.style.display = DisplayStyle.Flex;
            UiText.Bind(_title, $"place.{id}.title");
            UiText.Bind(_host, $"place.{id}.host");
            _art.style.backgroundImage = StyleKeyword.None;
            LoadArt(id, _art, () => CurrentPlace == id);   // a slow picture must not land on the next place

            _actions.Clear();
            _buttons.Clear();
            foreach (var action in place.Actions)
            {
                var a = action;
                var button = new Button(() => Do(a)) { name = "place-action-" + a.Id };
                button.AddToClassList("place-action");
                button.Add(UiText.Label("place.action." + a.Id, "place-action__label"));
                if (a.Kind == PlaceActionKind.Plan && _engine.Content.FreeActions.TryGetValue(a.FreeAction, out var free))
                {
                    var chips = new VisualElement();
                    chips.AddToClassList("place-action__chips");
                    AddChips(chips, free);
                    button.Add(chips);
                    button.Add(UiText.Label("place.planned", "place-action__planned"));
                }
                _buttons[a.Id] = button;
                _actions.Add(button);
            }
            Refresh();
        }

        /// <summary>What an action costs and gives, from its definition: nothing here is written by hand.</summary>
        void AddChips(VisualElement chips, FreeActionDef free)
        {
            string lang = _ui.Language;
            if (free.Cost.Cents > 0) chips.Add(Chip(null, Minus + MoneyFormat.Compact(free.Cost, lang), false));
            foreach (var e in free.Effects)
                switch (e)
                {
                    case CashEffect c: chips.Add(Chip(null, SignedMoney(c.Amount, lang), c.Amount.Cents >= 0)); break;
                    case ReserveEffect r: chips.Add(Chip("hud.reserve", SignedMoney(r.Amount, lang), r.Amount.Cents >= 0)); break;
                    case StatEffect s: chips.Add(Chip("stat." + s.Stat, Signed(s.Delta), s.Delta >= 0)); break;
                    case StudyXpEffect x: chips.Add(Chip("stat.knowledge", Signed(x.Amount), x.Amount >= 0)); break;
                }
        }

        static string Signed(int n) => n < 0 ? Minus + (-n) : "+" + n;

        static string SignedMoney(Money m, string lang) =>
            m.Cents < 0 ? Minus + MoneyFormat.Compact(Money.FromCents(-m.Cents), lang) : "+" + MoneyFormat.Compact(m, lang);

        static VisualElement Chip(string captionKey, string value, bool good)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            if (captionKey != null) chip.Add(UiText.Label(captionKey, "chip__caption"));
            var v = UiText.ValueLabel(value, "chip__value");
            v.AddToClassList(good ? "value--positive" : "value--negative");
            chip.Add(v);
            return chip;
        }

        void Do(PlaceAction a)
        {
            switch (a.Kind)
            {
                case PlaceActionKind.Plan: _plan.SetPlanned(a.FreeAction, !_plan.IsPlanned(a.FreeAction)); break;
                case PlaceActionKind.Open: _ui.Open(a.Tab); break;
                case PlaceActionKind.Loan: _ui.Loan.Open(Money.Zero); break;
            }
        }

        /// <summary>After the plan changed or a month passed: what is planned, what the points still allow, this month's line.</summary>
        public void Refresh()
        {
            var place = CurrentPlace != null ? Places.Find(CurrentPlace) : null;
            if (place == null) return;
            UiText.Bind(_line, $"place.{place.Id}.line.{(Math.Max(1, _state.Month) - 1) % Places.LinesPerPlace + 1}");
            _trust.text = _state.Npcs.Get(place.Host).ToString();
            _points.text = $"{_plan.PointsLeft} / {_plan.PointsTotal}";
            foreach (var a in place.Actions)
            {
                if (!_buttons.TryGetValue(a.Id, out var button)) continue;
                button.style.display = _state.Chapter >= a.MinChapter ? DisplayStyle.Flex : DisplayStyle.None;
                if (a.Kind != PlaceActionKind.Plan) continue;
                button.EnableInClassList("place-action--planned", _plan.IsPlanned(a.FreeAction));
                button.SetEnabled(_plan.CanPlan(a.FreeAction));
            }
        }

        static void LoadArt(string id, VisualElement target, Func<bool> stillWanted)
        {
            if (Art.TryGetValue(id, out var ready) && ready != null) { target.style.backgroundImage = ready; return; }
            var handle = Addressables.LoadAssetAsync<Texture2D>(Places.ArtAddress(id));
            handle.Completed += h =>
            {
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null) return;
                Art[id] = h.Result;
                if (stillWanted == null || stillWanted()) target.style.backgroundImage = h.Result;
            };
        }
    }
}
