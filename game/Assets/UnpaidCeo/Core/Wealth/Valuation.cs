using System;
using System.Collections.Generic;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Wealth
{
    public struct RoundResult
    {
        public Money CashIn;
        public double Ownership;
    }

    /// <summary>Stage 3 (chapters 4–5): what a company is worth and what the founder's share of it is worth.</summary>
    public static class Valuation
    {
        public const int TrailingMonths = 12;

        /// <summary>Operating profit of the last twelve months on record (fewer months are not annualized).</summary>
        public static Money TrailingProfit(IReadOnlyList<Money> monthlyOperatingProfit)
        {
            long cents = 0;
            int n = monthlyOperatingProfit?.Count ?? 0;
            for (int i = Math.Max(0, n - TrailingMonths); i < n; i++) cents += monthlyOperatingProfit[i].Cents;
            return Money.FromCents(cents);
        }

        /// <summary>Buyers pay more per dollar of profit for bigger companies; the public market pays the most.</summary>
        public static double Multiple(Money yearlyProfit, ValuationBalance b, bool isPublic)
        {
            if (isPublic) return b.PublicMultiple;
            foreach (var step in b.Ladder)
                if (yearlyProfit < step.Below) return step.Multiple;
            return b.TopMultiple;
        }

        public static Money CompanyValue(IReadOnlyList<Money> monthlyOperatingProfit, ValuationBalance b, bool isPublic)
        {
            var profit = TrailingProfit(monthlyOperatingProfit);
            return profit.Cents <= 0 ? Money.Zero : profit.MulRound(Multiple(profit, b, isPublic));
        }

        /// <summary>
        /// An investment round: new shares for RoundDilution of the company after the money is in, so the cash is
        /// value × d / (1 − d) and every earlier owner keeps (1 − d) of their share.
        /// </summary>
        public static RoundResult Round(Money companyValue, double ownership, ValuationBalance b)
        {
            double d = b.RoundDilution;
            return new RoundResult { CashIn = companyValue.MulRound(d / (1 - d)), Ownership = ownership * (1 - d) };
        }

        public static Money NetWorth(Money cash, Money companyValue, double ownership, Money debt) =>
            cash + companyValue.MulRound(ownership) - debt;
    }
}
