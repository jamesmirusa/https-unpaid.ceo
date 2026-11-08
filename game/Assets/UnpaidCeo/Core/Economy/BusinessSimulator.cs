using System;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    /// <summary>One month of one gig or business. Costs are paid this month; revenue arrives after ReceivableDelayMonths.</summary>
    public static class BusinessSimulator
    {
        public static BusinessMonthResult Simulate(BusinessState b, IndustryDef d, BusinessInputs i)
        {
            int capacity = d.Kind == IndustryKind.Gig
                ? (int)Math.Floor(d.CapacityPerWorkPoint * Math.Max(0, i.WorkShare) + 1e-9)
                : d.CapacityPerUnit * Math.Max(0, b.Units);

            int demand = EconomyFormulas.Demand(d.BaseDemand,
                EconomyFormulas.PriceFactor(b.Price, d.RefPrice, d.Elasticity),
                EconomyFormulas.MarketingFactor(b.Marketing, d.MarketingA, d.MarketingK),
                i.ReputationFactor, i.SeasonFactor, i.EventFactor);

            int units = EconomyFormulas.UnitsSold(demand, capacity);
            var revenue = Money.FromCents(b.Price.Cents * units);
            var cogs = Money.FromCents(d.UnitCost.Cents * units);
            var gross = revenue - cogs;
            var fixedCosts = d.Kind == IndustryKind.Business ? d.FixedMonthly : Money.Zero;
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
