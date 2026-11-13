using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>Bank loan: amount up to GameEngine.MaxLoan, a term from Balance.Loans.TermsMonths, the APR and the monthly payment.</summary>
    public sealed class LoanDialog
    {
        const int StepDollars = 100;

        readonly UiRoot _ui;
        readonly GameEngine _engine;
        readonly GameState _state;
        readonly SliderInt _amount;
        readonly Label _amountValue, _aprValue, _paymentValue;
        readonly List<Button> _terms = new List<Button>();
        int _months;
        VisualElement _panel;

        public VisualElement Root { get; }
        public bool IsOpen => Root.style.display == DisplayStyle.Flex;
        public IReadOnlyList<Button> TermButtons => _terms;
        public int MaxDollars => _amount.highValue;
        public int AmountDollars => _amount.value;
        public string AprText => _aprValue.text;
        public string PaymentText => _paymentValue.text;

        public event Action<Money, int> Taken;

        public LoanDialog(UiRoot ui, GameEngine engine, GameState state)
        {
            _ui = ui;
            _engine = engine;
            _state = state;
            Root = new VisualElement { name = "loan-dialog" };
            Root.AddToClassList("modal");
            var panel = _panel = new VisualElement();
            panel.AddToClassList("modal__panel");
            Root.Add(panel);

            panel.Add(UiText.Label("loan.title", "screen__title"));
            panel.Add(UiText.Label("loan.hint", "hint"));
            _amountValue = ValueRow(panel, "loan.amount", "loan-amount");
            _amount = new SliderInt(StepDollars, StepDollars) { name = "loan-slider" };
            _amount.RegisterValueChangedCallback(e =>
            {
                int snapped = Math.Max(_amount.lowValue, e.newValue / StepDollars * StepDollars);
                if (snapped != e.newValue) _amount.SetValueWithoutNotify(snapped);
                UpdateNumbers();
            });
            panel.Add(_amount);

            panel.Add(UiText.Label("loan.term", "section-title"));
            var terms = new VisualElement();
            terms.AddToClassList("choice-row");
            foreach (var m in engine.Content.Balance.Loans.TermsMonths)
            {
                int months = m;
                var b = UiText.Value(new Button(() => SelectTerm(months)) { name = "loan-term-" + months }, months.ToString());
                b.AddToClassList("choice-button");
                _terms.Add(b);
                terms.Add(b);
            }
            panel.Add(terms);

            _aprValue = ValueRow(panel, "loan.apr", "loan-apr");
            _paymentValue = ValueRow(panel, "loan.payment", "loan-payment");

            var take = UiText.Bind(new Button(() => Take()) { name = "loan-take" }, "loan.take");
            take.AddToClassList("primary-button");
            panel.Add(take);
            var cancel = UiText.Bind(new Button(Close) { name = "loan-cancel" }, "loan.cancel");
            cancel.AddToClassList("danger-button");
            panel.Add(cancel);
            Root.style.display = DisplayStyle.None;
        }

        static Label ValueRow(VisualElement parent, string key, string name)
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.Add(UiText.Label(key, "row__label"));
            var v = UiText.ValueLabel("", "row__value");
            v.name = name;
            row.Add(v);
            parent.Add(row);
            return v;
        }

        /// <summary>Opens with <paramref name="suggested"/> preselected (rounded up to $100, within what the bank lends).</summary>
        public void Open(Money suggested)
        {
            int max = (int)Math.Min(int.MaxValue, _engine.MaxLoan(_state).Cents / 100 / StepDollars * StepDollars);
            _amount.lowValue = Math.Min(StepDollars, Math.Max(0, max));
            _amount.highValue = Math.Max(_amount.lowValue, max);
            long want = (suggested.Cents + StepDollars * 100 - 1) / (StepDollars * 100) * StepDollars;
            _amount.SetValueWithoutNotify((int)Math.Max(_amount.lowValue, Math.Min(_amount.highValue, want)));
            var terms = _engine.Content.Balance.Loans.TermsMonths;
            SelectTerm(terms.Length > 0 ? terms[terms.Length - 1] : 12);
            Root.style.display = DisplayStyle.Flex;
            Kit.PopIn.Play(_panel);
            Root.BringToFront();
        }

        public void Close() => Root.style.display = DisplayStyle.None;

        void SelectTerm(int months)
        {
            _months = months;
            foreach (var b in _terms) b.EnableInClassList("choice-button--selected", b.name == "loan-term-" + months);
            UpdateNumbers();
        }

        Money Amount => Money.FromCents(_amount.value * 100L);

        void UpdateNumbers()
        {
            double apr = _engine.LoanApr(_state);
            _amountValue.text = MoneyFormat.Full(Amount, _ui.Language);
            _aprValue.text = $"{apr * 100:0.0}%";
            _paymentValue.text = MoneyFormat.Full(EconomyFormulas.LoanPayment(Amount, apr, Math.Max(1, _months)), _ui.Language);
        }

        /// <summary>Takes the loan as set; false when the bank refuses (nothing to lend).</summary>
        public bool Take()
        {
            var amount = Amount;
            if (!_engine.TakeLoan(_state, amount, _months)) return false;
            Close();
            Taken?.Invoke(amount, _months);
            return true;
        }
    }
}
