using System;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    /// <summary>One month of one gig or business. Costs are paid this month; revenue arrives after ReceivableDelayMonths.</summary>
    public static class BusinessSimulator
    {
        /// <summary>Share of full capacity the staff can run: the owner plus employees over the staff the units need.</summary>
        public static double Staffing(BusinessState b, IndustryDef d, double ownerShare = 1.0)
        {
            int needed = Math.Max(0, b.Units) * Math.Max(1, d.StaffPerUnit);
            double owner = Math.Max(0.0, Math.Min(1.0, ownerShare));
            return needed == 0 ? 0.0 : Math.Min(1.0, (Math.Max(0, b.Employees) + owner) / (double)needed);
        }

        /// <summary>Salaries of the employees plus employer overhead.</summary>
        public static Money Payroll(BusinessState b, IndustryDef d, double overhead) =>
            d.Salary.MulRound(Math.Max(0, b.Employees) * (1.0 + overhead));

        public static BusinessMonthResult Simulate(BusinessState b, IndustryDef d, BusinessInputs i)
        {
            int capacity = d.Kind == IndustryKind.Gig
                ? (int)Math.Floor(d.CapacityPerWorkPoint * Math.Max(0, i.WorkShare) + 1e-9)
                : (int)Math.Floor(d.CapacityPerUnit * Math.Max(0, b.Units) * Staffing(b, d, i.OwnerShare) + 1e-9);

            int demand = EconomyFormulas.Demand(d.BaseDemand,
                EconomyFormulas.PriceFactor(b.Price, d.RefPrice, d.Elasticity),
                EconomyFormulas.MarketingFactor(b.Marketing, d.MarketingA, d.MarketingK),
                i.ReputationFactor, i.SeasonFactor, i.EventFactor, i.DistrictFactor);

            int units = EconomyFormulas.UnitsSold(demand, capacity);
            var revenue = Money.FromCents(b.Price.Cents * units);
            var cogs = Money.FromCents(d.UnitCost.Cents * units);
            var gross = revenue - cogs;
            var fixedCosts = d.Kind == IndustryKind.Business
                ? Money.FromCents(d.FixedMonthly.Cents * Math.Max(0, b.Units)) + Payroll(b, d, i.PayrollOverhead)
                : Money.Zero;
            var marketing = b.Marketing;

            b.Receivables.Add(revenue);
            var collected = Money.Zero;
            if (b.Receivables.Count > Math.Max(0, d.ReceivableDelayMonths))
            {
                collected = b.Receivables[0];
                b.Receivables.RemoveAt(0);
            }

            return new BusinessMonthResult
            {
                UnitsSold = units,
                Revenue = revenue,
                Cogs = cogs,
                GrossProfit = gross,
                Fixed = fixedCosts,
                Marketing = marketing,
                OperatingProfit = gross - fixedCosts - marketing,
                CashCollected = collected,
            };
        }
    }
}
