using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Ui.Kit;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Stage 5B · the week calendar in the HUD: "Week 6 · 2 of 4", the week's action tokens, and the month's four weeks
    /// with their money dates from <see cref="MoneyCalendar"/> — the sum, green when today's cash covers it, red when it
    /// does not (a cash gap). A tap on a week says one line about cash gaps.
    /// </summary>
    public sealed class CalendarStrip
    {
        readonly UiRoot _ui;
        readonly VisualElement _tokens;
        readonly List<Button> _cells = new List<Button>();
        readonly List<Label> _heads = new List<Label>(), _names = new List<Label>(), _amounts = new List<Label>();
        List<CalendarWeek> _weeks;

        public VisualElement Root { get; }
        /// <summary>The week's action tokens (filled = left), placed by the HUD in its week pill.</summary>
        public VisualElement Tokens => _tokens;

        public CalendarStrip(UiRoot ui)
        {
            _ui = ui;
            Root = new VisualElement { name = "calendar" };
            Root.AddToClassList("calendar");
            // The week number lives in the HUD's week pill; the week's action tokens sit there too, under it.
            _tokens = new VisualElement { name = "calendar-tokens", pickingMode = PickingMode.Ignore };
            _tokens.AddToClassList("calendar__tokens");

            var row = new VisualElement();
            row.AddToClassList("calendar__row");
            for (int w = 1; w <= GameState.WeeksPerMonth; w++)
            {
                int week = w;
                var cell = new Button(() => Explain(week)) { name = "calendar-cell-" + w };
                cell.AddToClassList("calendar__cell");
                var h = UiText.Label("calendar.wk." + w, "calendar__cell-head");
                h.style.display = DisplayStyle.None;   // kept for screen readers' order; the "now" cell is marked by colour
                var n = UiText.Label("calendar.free", "calendar__cell-name");
                var a = UiText.ValueLabel("", "calendar__cell-amount");
                a.name = "calendar-amount-" + w;
                cell.Add(h); cell.Add(n); cell.Add(a);
                _heads.Add(h); _names.Add(n); _amounts.Add(a);
                _cells.Add(cell);
                row.Add(cell);
            }
            Root.Add(row);
        }

        public void Refresh(GameEngine engine, GameState state)
        {
            if (engine == null || state == null) return;
            var plan0 = _ui.DecisionsPanel;
            _weeks = MoneyCalendar.Month(state, engine.Content.Balance, plan0 != null ? plan0.PlannedCost : default);
            Root.style.display = _ui.RunMode ? DisplayStyle.None : DisplayStyle.Flex;   // challenge months pay all at once
            var plan = _ui.DecisionsPanel;
            int total = plan != null ? plan.PointsTotal : 2, left = plan != null ? plan.PointsLeft : total;
            _tokens.Clear();
            for (int i = 0; i < total; i++)
            {
                var t = new VisualElement { pickingMode = PickingMode.Ignore };
                t.AddToClassList("calendar__token");
                t.EnableInClassList("calendar__token--spent", i >= left);
                _tokens.Add(t);
            }
            for (int i = 0; i < _weeks.Count && i < _cells.Count; i++)
            {
                var w = _weeks[i];
                var cell = _cells[i];
                cell.EnableInClassList("calendar__cell--now", w.Now);
                cell.EnableInClassList("calendar__cell--past", w.Past);
                cell.EnableInClassList("calendar__cell--ok", !w.Past && w.Headline != null && w.Covered);
                cell.EnableInClassList("calendar__cell--gap", !w.Past && w.Headline != null && !w.Covered);
                UiText.Bind(_heads[i], w.Now ? "calendar.now" : "calendar.wk." + w.WeekOfMonth);
                UiText.Bind(_names[i], w.Headline != null ? "calendar.name." + w.Headline : "calendar.free");
                _amounts[i].text = w.Headline != null ? EffectChips.Minus + MoneyFormat.Compact(w.Total, _ui.Language) : "—";
            }
        }

        /// <summary>One line from life about the week tapped: a gap, or what that money date is.</summary>
        void Explain(int weekOfMonth)
        {
            if (_weeks == null) return;
            var w = _weeks[weekOfMonth - 1];
            string key = w.Headline == null ? "calendar.tip.free"
                : !w.Past && !w.Covered ? "calendar.tip.gap"
                : "calendar.tip." + w.Headline;
            Toasts.Show(_ui.Root, key, w.Headline == null ? "money_date" : !w.Covered && !w.Past ? "mood_burnout" : IconOf(w.Headline), 4000);
        }

        static string IconOf(string headline) => headline switch
        {
            "bill.room" => "rent", "loan.pay" => "loan", "bill.essentials" => "money_date", _ => "food",
        };
    }
}
