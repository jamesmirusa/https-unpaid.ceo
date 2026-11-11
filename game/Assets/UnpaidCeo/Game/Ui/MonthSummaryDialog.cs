using System;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// The month's result as a popup on the main screen: revenue, net profit, living costs, cash, and one line that
    /// says what the numbers mean. "Got it" goes on with the game; "Full report" opens the company screen.
    /// </summary>
    public sealed class MonthSummaryDialog
    {
        readonly Label _title, _month, _revenue, _profit, _personal, _cash, _change, _tip;

        public VisualElement Root { get; }
        public bool IsOpen => Root.style.display == DisplayStyle.Flex;
        /// <summary>The player asked for the full report.</summary>
        public event Action DetailsRequested;
        public event Action Closed;

        public MonthSummaryDialog()
        {
            Root = new VisualElement { name = "month-summary" };
            Root.AddToClassList("modal");
            var panel = new VisualElement();
            panel.AddToClassList("modal__panel");
            var head = new VisualElement();
            head.AddToClassList("summary__head");
            _title = UiText.Label("summary.title", "section-title");
            _month = UiText.ValueLabel("", "section-title");
            _month.name = "summary-month";
            head.Add(_title);
            head.Add(_month);
            panel.Add(head);
            _revenue = Row(panel, "summary.revenue", "summary-revenue");
            _profit = Row(panel, "summary.profit", "summary-profit");
            _personal = Row(panel, "summary.personal", "summary-personal");
            _cash = Row(panel, "summary.cash", "summary-cash", total: true);
            _change = Row(panel, "summary.change", "summary-change");
            _tip = UiText.Label("summary.tip.profit", "summary__tip");
            _tip.name = "summary-tip";
            panel.Add(_tip);
            var ok = UiText.Bind(new Button(Close) { name = "month-summary-ok" }, "summary.ok");
            ok.AddToClassList("primary-button");
            panel.Add(ok);
            var details = UiText.Bind(new Button(() => { Close(); DetailsRequested?.Invoke(); }) { name = "month-summary-details" }, "summary.details");
            details.AddToClassList("link-button");
            panel.Add(details);
            Root.Add(panel);
            Root.style.display = DisplayStyle.None;
        }

        static Label Row(VisualElement panel, string captionKey, string valueName, bool total = false)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            if (total) row.AddToClassList("row--total");
            row.Add(UiText.Label(captionKey, "row__label"));
            var value = UiText.ValueLabel("", "row__value");
            value.name = valueName;
            row.Add(value);
            panel.Add(row);
            return value;
        }

        /// <summary>The one thing this month's numbers teach.</summary>
        public static string TipKey(MonthReport report)
        {
            if (report.Revenue <= Money.Zero) return "summary.tip.no_revenue";
            if (report.NetProfit < Money.Zero) return "summary.tip.loss";
            if (report.CashEnd < report.CashStart) return "summary.tip.cash_down";
            return "summary.tip.profit";
        }

        public void Show(MonthReport report, string language)
        {
            _month.text = report.Month.ToString();
            Set(_revenue, report.Revenue, language, signed: false);
            Set(_profit, report.NetProfit, language, signed: true);
            Set(_personal, report.PersonalCosts, language, signed: false);
            Set(_cash, report.CashEnd, language, signed: true);
            var change = report.CashEnd - report.CashStart;
            Set(_change, change, language, signed: true);
            UiText.Bind(_tip, TipKey(report));
            Root.style.display = DisplayStyle.Flex;
            Root.BringToFront();
        }

        static void Set(Label label, Money amount, string language, bool signed)
        {
            label.text = MoneyFormat.Compact(amount, language);
            label.EnableInClassList("value--negative", signed && amount < Money.Zero);
            label.EnableInClassList("value--positive", signed && amount > Money.Zero);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Root.style.display = DisplayStyle.None;
            Closed?.Invoke();
        }
    }
}
