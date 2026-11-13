using System.Linq;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Company tab: last month's P&L as a waterfall and as rows (interest, capex and loan principal from chapter 2),
    /// unit economics of the business, the bank loans, and the simulated-money disclaimer.
    /// </summary>
    public sealed class CompanyScreen
    {
        /// <summary>Row key, value, total row, shown only when not zero (chapter 2–3 rows stay out of chapter 1).</summary>
        static readonly (string key, System.Func<MonthReport, Money> value, bool total, bool optional)[] Rows =
        {
            ("company.revenue", r => r.Revenue, false, false),
            ("company.cogs", r => -r.Cogs, false, false),
            ("company.gross", r => r.GrossProfit, true, false),
            ("company.fixed", r => -r.Fixed, false, false),
            ("company.marketing", r => -r.Marketing, false, false),
            ("company.mistake", r => -r.MistakeLoss, false, false),
            ("company.interest", r => -r.Interest, false, true),
            ("company.tax", r => -r.TaxReserve, false, false),
            ("company.net", r => r.NetProfit, true, false),
            ("company.personal", r => -r.PersonalCosts, false, false),
            ("company.capex", r => -r.Capex, false, true),
            ("company.asset_sales", r => r.AssetSales, false, true),
            ("company.loan_principal", r => -(r.LoanPayments - r.Interest), false, true),
            ("company.cash_end", r => r.CashEnd, true, false),
        };

        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly VisualElement _rows, _unit, _loans;
        readonly Label _empty;
        readonly PnlWaterfall _chart = new PnlWaterfall();
        readonly Label[] _values = new Label[Rows.Length];
        readonly VisualElement[] _rowElements = new VisualElement[Rows.Length];
        MonthReport _report;

        public CompanyScreen(VisualElement root, UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            UiText.Bind(root.Q<Label>("company-title"), "company.title");
            _empty = UiText.Bind(root.Q<Label>("company-empty"), "company.empty");
            UiText.Bind(root.Q<Label>("company-disclaimer"), "company.disclaimer");
            root.Q("pnl-chart").Add(_chart);
            _rows = root.Q("pnl-rows");
            _unit = root.Q("unit-economics");
            _loans = root.Q("loans");
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
                _rowElements[i] = row;
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
            if (has)
                for (int i = 0; i < Rows.Length; i++)
                {
                    var m = Rows[i].value(_report);
                    _values[i].text = MoneyFormat.Full(m, _ui.Language);
                    _values[i].EnableInClassList("value--negative", m.Cents < 0);
                    _rowElements[i].style.display = Rows[i].optional && m.Cents == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                }
            RefreshUnitEconomics();
            RefreshLoans();
        }

        void RefreshUnitEconomics()
        {
            _unit.Clear();
            var business = _state.Businesses.FirstOrDefault(b => b.Units > 0
                && _engine.Content.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == IndustryKind.Business);
            BusinessMonthResult last = null;
            if (business == null || _report == null || !_report.BusinessResults.TryGetValue(business.Id, out last))
            {
                _unit.style.display = DisplayStyle.None;
                return;
            }
            var def = _engine.Content.Industries[business.IndustryId];
            var v = UnitEconomics.Compute(business, def, last, _engine.Content.Balance.PayrollOverhead);
            _unit.Add(UiText.Label("company.unit_title", "section-title"));
            Row(_unit, "company.margin", $"{System.Math.Round(v.GrossMargin * 100)}%", "unit-margin");
            Row(_unit, "company.break_even", v.BreakEvenUnits == UnitEconomicsView.NeverBreaksEven ? "—" : v.BreakEvenUnits.ToString(), "unit-break-even");
            Row(_unit, "company.cac", MoneyFormat.Full(v.Cac, _ui.Language), "unit-cac");
            Row(_unit, "company.ltv", MoneyFormat.Full(v.Ltv, _ui.Language), "unit-ltv");
            Row(_unit, "company.ltv_cac", double.IsPositiveInfinity(v.LtvToCac) ? "∞" : $"{v.LtvToCac:0.0}", "unit-ltv-cac");
            _unit.style.display = DisplayStyle.Flex;
        }

        void RefreshLoans()
        {
            _loans.Clear();
            if (_state.Chapter < 2)
            {
                _loans.style.display = DisplayStyle.None;
                return;
            }
            _loans.Add(UiText.Label("company.loans_title", "section-title"));
            foreach (var loan in _state.Loans)
                Row(_loans, "company.loan_row",
                    $"{MoneyFormat.Compact(loan.Balance, _ui.Language)} · {loan.MonthsLeft} · {MoneyFormat.Compact(loan.Payment, _ui.Language)}", "loan-" + loan.Id);
            var take = UiText.Bind(new Button(() => _ui.Loan.Open(Money.Zero)) { name = "company-take-loan" }, "company.take_loan");
            take.AddToClassList("primary-button");
            _loans.Add(take);
            _loans.style.display = DisplayStyle.Flex;
        }

        static void Row(VisualElement parent, string key, string value, string name)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.Add(UiText.Label(key, "row__label"));
            var v = UiText.ValueLabel(value, "row__value");
            v.name = name;
            row.Add(v);
            parent.Add(row);
        }
    }
}
