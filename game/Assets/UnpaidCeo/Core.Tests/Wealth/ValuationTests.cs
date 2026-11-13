using System.Collections.Generic;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Core.Tests.Game;
using UnpaidCeo.Core.Wealth;

namespace UnpaidCeo.Core.Tests.Wealth
{
    /// <summary>Stage 3: a company is worth a multiple of its yearly profit, and the founder owns a share of it.</summary>
    public class ValuationTests
    {
        static ValuationBalance B => BotRunner.Content().Balance.Valuation;
        static Money Usd(long dollars) => Money.FromCents(dollars * 100);

        [Test]
        public void Bigger_profits_earn_bigger_multiples()
        {
            Assert.AreEqual(2.0, Valuation.Multiple(Usd(150_000), B, isPublic: false));
            Assert.AreEqual(2.7, Valuation.Multiple(Usd(500_000), B, false));
            Assert.AreEqual(3.3, Valuation.Multiple(Usd(1_500_000), B, false));
            Assert.AreEqual(5.3, Valuation.Multiple(Usd(20_000_000), B, false));
            Assert.AreEqual(8.0, Valuation.Multiple(Usd(60_000_000), B, false));
            Assert.AreEqual(15.0, Valuation.Multiple(Usd(60_000_000), B, isPublic: true));
        }

        [Test]
        public void A_company_is_worth_its_last_twelve_months_of_profit_times_the_multiple()
        {
            var months = new List<Money>();
            for (int i = 0; i < 14; i++) months.Add(Usd(i < 2 ? 1_000_000 : 20_000));   // the two oldest months fall out
            Assert.AreEqual(Usd(240_000), Valuation.TrailingProfit(months));
            Assert.AreEqual(Usd(480_000), Valuation.CompanyValue(months, B, false));

            Assert.AreEqual(Money.Zero, Valuation.CompanyValue(new List<Money> { Usd(-5_000), Usd(2_000) }, B, false), "a loss-making year is worth nothing");
            Assert.AreEqual(Usd(40_000), Valuation.CompanyValue(new List<Money> { Usd(20_000) }, B, false), "one good month is not annualized");
        }

        [Test]
        public void A_round_trades_a_fifth_of_the_company_for_cash()
        {
            var round = Valuation.Round(Usd(4_000_000), ownership: 1.0, B);
            Assert.AreEqual(Usd(1_000_000), round.CashIn, "20% of the post-money company: 4M / 0.8 × 0.2");
            Assert.AreEqual(0.8, round.Ownership, 1e-9);
            Assert.AreEqual(0.64, Valuation.Round(Usd(4_000_000), round.Ownership, B).Ownership, 1e-9);
        }

        [Test]
        public void Net_worth_counts_the_founders_share_only()
        {
            Assert.AreEqual(Usd(50_000 + 800_000 - 30_000), Valuation.NetWorth(Usd(50_000), Usd(1_000_000), 0.8, Usd(30_000)));
        }

        [Test]
        public void Valuation_numbers_carry_their_sources()
        {
            var sources = BotRunner.Content().Balance.Sources;
            Assert.IsTrue(sources.Exists(s => s.Field == "valuation.ladder" && s.Url.Contains("bizbuysell")));
            Assert.IsTrue(sources.Exists(s => s.Field == "valuation.roundDilution" && s.Url.Contains("carta")));
            Assert.IsTrue(sources.Exists(s => s.Field == "valuation.publicMultiple" && s.Note.Contains("assumption")));
        }
    }
}
