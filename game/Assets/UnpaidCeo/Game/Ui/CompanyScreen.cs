using UnityEngine.UIElements;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Company tab: last month's P&L as a waterfall and as rows, with the simulated-money disclaimer.</summary>
    public sealed class CompanyScreen
    {
        static readonly (string key, System.Func<MonthReport, Money> value, bool total)[] Rows =
        {
            ("company.revenue", r => r.Revenue, false),
            ("company.cogs", r => -r.Cogs, false),
            ("company.gross", r => r.GrossProfit, true),
            ("company.fixed", r => -r.Fixed, false),
            ("company.marketing", r => -r.Marketing, false),
            ("company.mistake", r => -r.MistakeLoss, false),
            ("company.tax", r => -r.TaxReserve, false),
            ("company.net", r => r.NetProfit, true),
            ("company.personal", r => -r.PersonalCosts, false),
            ("company.cash_end", r => r.CashEnd, true),
        };

        readonly UiRoot _ui;
        readonly VisualElement _rows;
        readonly Label _empty;
        readonly PnlWaterfall _chart = new PnlWaterfall();
        readonly Label[] _values = new Label[Rows.Length];
        MonthReport _report;

        public CompanyScreen(VisualElement root, UiRoot ui)
        {
            _ui = ui;
            UiText.Bind(root.Q<Label>("company-title"), "company.title");
            _empty = UiText.Bind(root.Q<Label>("company-empty"), "company.empty");
            UiText.Bind(root.Q<Label>("company-disclaimer"), "company.disclaimer");
            root.Q("pnl-chart").Add(_chart);
            _rows = root.Q("pnl-rows");
            for (int i = 0; i < Rows.Length; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("row");
                if (Rows[i].total) row.AddToClassList("row--total");
                row.Add(UiText.Label(Rows[i].key, "row__label"));
                _values[i] = UiText.ValueLabel("", "row__value");
                _values[i].name = "value-" + Rows[i].key;
                row.Add(_values[i]);
                _rows.Add(row);
            }
            Refresh();
        }

        public void Show(MonthReport report)
        {
            _report = report;
            Refresh();
        }

        public void Refresh()
        {
            bool has = _report != null;
            _empty.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            _rows.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            _chart.SetReport(_report);
            if (!has) return;
            for (int i = 0; i < Rows.Length; i++)
            {
                var m = Rows[i].value(_report);
                _values[i].text = MoneyFormat.Full(m, _ui.Language);
                _values[i].EnableInClassList("value--negative", m.Cents < 0);
            }
        }
    }
}
