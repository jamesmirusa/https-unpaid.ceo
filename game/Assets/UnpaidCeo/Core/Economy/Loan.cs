using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Economy
{
    /// <summary>A bank loan repaid in equal monthly (annuity) payments.</summary>
    public sealed class LoanState
    {
        public string Id { get; set; }
        public Money Principal { get; set; }
        /// <summary>Principal still owed.</summary>
        public Money Balance { get; set; }
        public double Apr { get; set; }
        public int MonthsLeft { get; set; }
        public Money Payment { get; set; }
    }
}
