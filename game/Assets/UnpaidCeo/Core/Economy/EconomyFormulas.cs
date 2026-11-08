using System;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    /// <summary>Pure business formulas (spec §2). Money in cents; unit counts floor.</summary>
    public static class EconomyFormulas
    {
        // Guards floor() against products like 0.1 × 3 × 1000 = 299.99999999999994.
        const double FloorEpsilon = 1e-9;

        /// <summary>(price / refPrice)^(−elasticity).</summary>
        public static double PriceFactor(Money price, Money refPrice, double elasticity)
        {
            if (refPrice.Cents <= 0) throw new ArgumentOutOfRangeException(nameof(refPrice));
            if (price.Cents <= 0) return 0.0;
            return Math.Pow((double)price.Cents / refPrice.Cents, -elasticity);
        }

        /// <summary>1 + a·ln(1 + spend / k): diminishing returns on marketing.</summary>
        public static double MarketingFactor(Money spend, double a, Money k)
        {
            if (k.Cents <= 0) throw new ArgumentOutOfRangeException(nameof(k));
            if (spend.Cents <= 0) return 1.0;
            return 1.0 + a * Math.Log(1.0 + (double)spend.Cents / k.Cents);
        }

        /// <summary>floor(base × Π factors), never negative.</summary>
        public static int Demand(int baseDemand, params double[] factors)
        {
            double v = baseDemand;
            foreach (var f in factors) v *= f;
            if (v <= 0) return 0;
            double floored = Math.Floor(v + FloorEpsilon);
            return floored >= int.MaxValue ? int.MaxValue : (int)floored;
        }

        public static int UnitsSold(int demand, int capacity) => Math.Max(0, Math.Min(demand, capacity));

        public static double GrossMargin(Money price, Money unitCost) =>
            price.Cents <= 0 ? 0.0 : (double)(price.Cents - unitCost.Cents) / price.Cents;

        /// <summary>Budgeting reserve on positive operating profit; zero on a loss.</summary>
        public static Money TaxReserve(Money operatingProfit, double rate) =>
            operatingProfit.Cents <= 0 ? Money.Zero : operatingProfit.MulRound(rate);

        /// <summary>ceil(fixed / (price − unitCost)); int.MaxValue when each sale loses money.</summary>
        public static int BreakEvenUnits(Money fixedMonthly, Money price, Money unitCost)
        {
            long perUnit = price.Cents - unitCost.Cents;
            if (perUnit <= 0) return int.MaxValue;
            if (fixedMonthly.Cents <= 0) return 0;
            long units = (fixedMonthly.Cents + perUnit - 1) / perUnit;
            return units >= int.MaxValue ? int.MaxValue : (int)units;
        }

        /// <summary>Annuity payment per month; principal / months when APR is 0.</summary>
        public static Money LoanPayment(Money principal, double apr, int months)
        {
            if (months <= 0) throw new ArgumentOutOfRangeException(nameof(months));
            if (apr <= 0) return principal.MulRound(1.0 / months);
            double r = apr / 12.0;
            double payment = principal.Cents * r / (1.0 - Math.Pow(1.0 + r, -months));
            return Money.FromCents((long)Math.Round(payment, MidpointRounding.AwayFromZero));
        }
    }
}
