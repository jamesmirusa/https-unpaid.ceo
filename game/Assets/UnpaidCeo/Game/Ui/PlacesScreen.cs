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
            foreach (var t in _thumbs) LoadArt(Places.ThumbAddress(t.Key), t.Value, null);
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
            LoadArt(Places.ArtAddress(id), _art, () => CurrentPlace == id);   // a slow picture must not land on the next place

            _actions.Clear();
            _buttons.Clear();
            foreach (var action in place.Actions)
            {
                var a = action;
                Button button;
                if (a.Kind == PlaceActionKind.Plan && _engine.Content.FreeActions.TryGetValue(a.FreeAction, out var free))
                {
                    // The same card as in the rooms and on the Week screen: done says done, closed says why.
                    var card = new ChoiceCard("place-action-" + a.Id, "place-status-" + a.Id, "place.action." + a.Id, () => Do(a)).WithIcon(Kit.GameIconFor.Action(a.FreeAction));
                    card.SetChips(free.Cost, free.Effects, _ui.Language);
                    card.Confirm = free.Confirm;
                    button = card;
                }
                else
                {
                    button = new Button(() => Do(a)) { name = "place-action-" + a.Id };
                    button.AddToClassList("place-action");
                    button.Add(UiText.Label("place.action." + a.Id, "place-action__label"));
                }
                _buttons[a.Id] = button;
                _actions.Add(button);
            }
            Refresh();
        }

        void Do(PlaceAction a)
        {
            switch (a.Kind)
            {
                case PlaceActionKind.Plan: _plan.Plan(a.FreeAction); break;
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
                if (!(button is ChoiceCard card)) continue;
                var state = _plan.State(a.FreeAction);
                card.Show(state, state == ChoiceState.Open && _plan.Recommended(a.FreeAction), _plan.WeeksUntilOpen(a.FreeAction));
            }
        }

        /// <summary>Loads a picture once (kept for the session) and shows it if it is still wanted when it arrives.</summary>
        /// <summary>A place's light picture onto any element (the signs over the buildings use it).</summary>
        public static void LoadThumb(string id, VisualElement target) => LoadArt(Places.ThumbAddress(id), target, null);

        static void LoadArt(string address, VisualElement target, Func<bool> stillWanted)
        {
            if (Art.TryGetValue(address, out var ready) && ready != null) { target.style.backgroundImage = ready; return; }
            var handle = Addressables.LoadAssetAsync<Texture2D>(address);
            handle.Completed += h =>
            {
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null) return;
                Art[address] = h.Result;
                if (stillWanted == null || stillWanted()) target.style.backgroundImage = h.Result;
            };
        }
    }
}
