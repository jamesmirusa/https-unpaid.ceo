using System;
using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Chapter 2's first decision: six business cards (name, scale path, capex, margin, break-even, main lesson) and the
    /// simulated-money disclaimer. Choosing calls GameEngine.ChooseIndustry and offers to open the first unit; when cash does
    /// not cover the capex plus the months before clients pay (GameEngine.FirstUnitBudget) the loan dialog opens too,
    /// pre-filled with GameEngine.SuggestedFirstUnitLoan.
    /// </summary>
    public sealed class IndustryScreen
    {
        readonly UiRoot _ui;
        readonly VisualElement _cards, _offer;
        GameEngine _engine;
        GameState _state;

        public event Action<string> Chosen;

        public IndustryScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            UiText.Bind(root.Q<Label>("industry-title"), "industry.title");
            UiText.Bind(root.Q<Label>("industry-hint"), "industry.hint");
            UiText.Bind(root.Q<Label>("industry-disclaimer"), "industry.disclaimer");
            _cards = root.Q("industry-cards");
            _offer = root.Q("industry-offer");
            _offer.style.display = DisplayStyle.None;
            Show(engine, state);
        }

        string Chosen0 => _state.Businesses.Select(b => b.IndustryId)
            .FirstOrDefault(id => _engine.Content.Industries.TryGetValue(id, out var d) && d.Kind == IndustryKind.Business);

        public void Show(GameEngine engine, GameState state)
        {
            _engine = engine;
            _state = state;
            _cards.Clear();
            var chosen = Chosen0;
            foreach (var id in engine.Content.Balance.Chapter2Industries)
                if (engine.Content.Industries.TryGetValue(id, out var def)) _cards.Add(Card(def, chosen));
        }

        VisualElement Card(IndustryDef def, string chosen)
        {
            var id = def.Id;
            var card = new VisualElement { name = "card-" + id };
            card.AddToClassList("industry-card");
            card.EnableInClassList("industry-card--chosen", chosen == id);
            card.Add(UiText.Label($"industry.{id}.name", "industry-card__name"));

            var scale = new VisualElement();
            scale.AddToClassList("industry-card__scale");
            foreach (var key in def.ScaleNameKeys ?? new System.Collections.Generic.List<string>())
                scale.Add(UiText.Label(key, "industry-card__step"));
            card.Add(scale);

            var probe = new BusinessState { Id = id, IndustryId = id, Price = def.Price, Units = 1 };
            var unit = UnitEconomics.Compute(probe, def, null, _engine.Content.Balance.PayrollOverhead);
            card.Add(Row("industry.capex", MoneyFormat.Compact(def.Capex, _ui.Language), "capex-" + id));
            card.Add(Row("industry.margin", $"{Math.Round(unit.GrossMargin * 100)}%", "margin-" + id));
            card.Add(Row("industry.break_even", unit.BreakEvenUnits == UnitEconomicsView.NeverBreaksEven ? "—" : unit.BreakEvenUnits.ToString(), "break-even-" + id));
            if (!string.IsNullOrEmpty(def.LessonId))
            {
                var lesson = new VisualElement();
                lesson.AddToClassList("row");
                lesson.Add(UiText.Label("industry.lesson", "row__label"));
                lesson.Add(UiText.Label($"lesson.{def.LessonId}.title", "industry-card__lesson", AcademyScreen.Table));
                card.Add(lesson);
            }

            var choose = UiText.Bind(new Button(() => Choose(id)) { name = "choose-" + id }, chosen == id ? "industry.chosen" : "industry.choose");
            choose.AddToClassList("primary-button");
            choose.AddToClassList("industry-card__choose");
            choose.SetEnabled(chosen == null && _state.Chapter >= 2);
            card.Add(choose);
            return card;
        }

        static VisualElement Row(string key, string value, string valueName)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.Add(UiText.Label(key, "row__label"));
            var v = UiText.ValueLabel(value, "row__value");
            v.name = valueName;
            row.Add(v);
            return row;
        }

        /// <summary>Picks the business; false when the engine refuses (chapter 1, already chosen, not a chapter-2 industry).</summary>
        public bool Choose(string id)
        {
            if (!_engine.ChooseIndustry(_state, id)) return false;
            var def = _engine.Content.Industries[id];
            Show(_engine, _state);
            Chosen?.Invoke(id);
            ShowOffer(id, def.Capex);
            if (_state.Cash < _engine.FirstUnitBudget(_state, def)) _ui.Loan.Open(_engine.SuggestedFirstUnitLoan(_state, id));
            return true;
        }

        void ShowOffer(string id, Money capex)
        {
            _offer.Clear();
            _offer.Add(UiText.Label("industry.offer", "industry-offer__text"));
            var open = UiText.Bind(new Button(() =>
            {
                _ui.DecisionsPanel.QueueUnits(id, 1);
                _offer.style.display = DisplayStyle.None;
                _ui.Open(UiRoot.Decisions);
            }) { name = "open-first-unit" }, "industry.open_first");
            open.AddToClassList("primary-button");
            _offer.Add(open);
            _offer.Add(UiText.ValueLabel(MoneyFormat.Compact(capex, _ui.Language), "industry-offer__capex"));
            _offer.style.display = DisplayStyle.Flex;
        }
    }
}
