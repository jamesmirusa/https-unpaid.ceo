using System;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    /// <summary>Per-unit numbers for one business: what each sale earns, how many sales cover the month, what a customer costs and brings.</summary>
    public sealed class UnitEconomicsView
    {
        /// <summary>BreakEvenUnits when each sale earns nothing or loses money: no volume covers the fixed costs.</summary>
        public const int NeverBreaksEven = -1;

        /// <summary>(price − unit cost) / price; 0 when the price is 0.</summary>
        public double GrossMargin { get; set; }
        /// <summary>Sales a month that cover fixed costs and salaries, or NeverBreaksEven.</summary>
        public int BreakEvenUnits { get; set; }
        /// <summary>Customer acquisition cost: last month's marketing per new customer.</summary>
        public Money Cac { get; set; }
        /// <summary>Lifetime value: margin per sale × purchases per customer.</summary>
        public Money Ltv { get; set; }
        /// <summary>Ltv / Cac; +∞ with no marketing spend.</summary>
        public double LtvToCac { get; set; }
    }

    public static class UnitEconomics
    {
        /// <param name="last">Last month's result for this business (sales and marketing); null before its first month.</param>
        public static UnitEconomicsView Compute(BusinessState b, IndustryDef d, BusinessMonthResult last, double payrollOverhead)
        {
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (d == null) throw new ArgumentNullException(nameof(d));

            var marginPerSale = b.Price - d.UnitCost;
            // A business with no unit yet is judged as if it opened its first one.
            var fixedCosts = Money.FromCents(d.FixedMonthly.Cents * Math.Max(1, b.Units)) + BusinessSimulator.Payroll(b, d, payrollOverhead);
            int breakEven = marginPerSale.Cents <= 0
                ? UnitEconomicsView.NeverBreaksEven
                : (int)Math.Min(int.MaxValue, (fixedCosts.Cents + marginPerSale.Cents - 1) / marginPerSale.Cents);

            double repeat = Math.Max(1.0, d.RepeatPurchases);
            int newCustomers = last == null ? 0 : (int)Math.Round(last.UnitsSold / repeat, MidpointRounding.AwayFromZero);
            var marketing = last?.Marketing ?? b.Marketing;
            var cac = Money.FromCents((long)Math.Round(marketing.Cents / (double)Math.Max(1, newCustomers), MidpointRounding.AwayFromZero));
            var ltv = marginPerSale.MulRound(repeat);

            return new UnitEconomicsView
            {
                GrossMargin = b.Price.Cents <= 0 ? 0.0 : marginPerSale.Cents / (double)b.Price.Cents,
                BreakEvenUnits = breakEven,
                Cac = cac,
                Ltv = ltv,
                LtvToCac = cac.Cents == 0 ? double.PositiveInfinity : ltv.Cents / (double)cac.Cents,
            };
        }
    }
}
