using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Persistence;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>Regressions from the final branch review: exploits, unreachable facts, bankruptcy loop, report math, save repair.</summary>
    public class ReviewFixTests
    {
        const string FreeActions = @"{ ""freeActions"": [
  { ""id"": ""loan"",  ""actionPoints"": 0, ""cost"": 0,    ""effects"": [ { ""cash"": 100000 }, { ""setFact"": ""shark_debt"" } ] },
  { ""id"": ""treat"", ""actionPoints"": 0, ""cost"": 5000, ""effects"": [] }
] }";

        static ContentDb Db(double errorChance = 0.0) => ContentDb.FromJson(new ContentFiles
        {
            Industries = TestContent.Industries(1.0),
            Balance = TestContent.Balance(errorChance),
            Storylets = TestContent.Storylets,
            FreeActions = FreeActions,
        });

        static (GameEngine engine, GameState state) NewGame(double errorChance = 0.0)
        {
            var engine = new GameEngine(Db(errorChance));
            return (engine, engine.NewGame(7, TestContent.Hero));
        }

        [Test]
        public void Debt_free_action_cannot_be_repeated_while_its_fact_is_open()
        {
            var (engine, s) = NewGame();
            engine.EndMonth(s, TestContent.Decisions(0, 0, 100, 0, "loan"));
            var cash = s.Cash;
            var report = engine.EndMonth(s, TestContent.Decisions(0, 0, 100, 0, "loan"));
            CollectionAssert.Contains(report.RejectedActions, "loan");
            Assert.AreEqual(1, s.Facts.MonthSet("shark_debt"), "the debt keeps its original month so collection comes due");
            Assert.LessOrEqual(s.Cash.Cents, cash.Cents, "no second $1,000");
        }

        [Test]
        public void Duplicate_free_actions_in_one_month_count_once()
        {
            var (engine, s) = NewGame();
            s.Cash = Money.FromDollars(1000);   // a free action is paid for: the hero can afford one treat
            var report = engine.EndMonth(s, TestContent.Decisions(0, 0, 100, 0, "treat", "treat", "treat"));
            Assert.AreEqual(2, report.RejectedActions.Count(a => a == "treat"));
        }

        [Test]
        public void Work_split_above_100_does_not_multiply_capacity()
        {
            var (engine, a) = NewGame();
            var normal = engine.EndMonth(a, TestContent.Decisions(60, 10, 20, 10));
            var (engine2, b) = NewGame();
            var d = TestContent.Decisions(60, 10, 20, 10);
            d.WorkSplit["gig_test"] = 300;
            var inflated = engine2.EndMonth(b, d);
            Assert.AreEqual(normal.Revenue, inflated.Revenue);
        }

        [Test]
        public void Scenes_are_rolled_once_per_month_even_after_a_reload()
        {
            var (engine, s) = NewGame();
            engine.BeginMonth(s);
            s.PendingStorylets.Clear(); // every scene of the month resolved
            var reloaded = SaveCodec.Decode(SaveCodec.Encode(s)).State;
            Assert.IsEmpty(engine.BeginMonth(reloaded), "a relaunch must not roll new scenes for the same month");
            Assert.IsEmpty(reloaded.PendingStorylets);
        }

        [Test]
        public void Month_that_spends_more_than_it_earns_is_the_first_loss()
        {
            var (engine, s) = NewGame();
            s.Cash = Money.FromDollars(100);   // the $50 treat is paid from cash on hand
            var report = engine.EndMonth(s, TestContent.Decisions(0, 0, 100, 0, "treat"));
            Assert.Less(report.CashEnd.Cents, report.CashStart.Cents);
            Assert.IsTrue(s.Facts.Has("first_loss"));
        }

        [Test]
        public void Clearing_bankrupt_restarts_the_deep_debt_count()
        {
            var (engine, s) = NewGame();
            s.Cash = engine.Content.Balance.DebtBankruptLine - Money.FromDollars(1000);
            s.MonthsInDeepDebt = 2; // was bankrupt; the comeback just cleared the fact
            engine.EndMonth(s, TestContent.Decisions(0, 0, 100, 0));
            Assert.IsFalse(s.Facts.Has("bankrupt"), "the comeback month must not re-bankrupt the player at once");
        }

        [Test]
        public void Solvent_player_drops_a_stale_bankrupt_fact()
        {
            var (engine, s) = NewGame();
            s.Facts.Set("bankrupt", 1);
            s.Cash = Money.FromDollars(50000);
            engine.EndMonth(s, TestContent.Decisions(0, 0, 100, 0));
            Assert.IsFalse(s.Facts.Has("bankrupt"));
        }

        [Test]
        public void Report_adds_up_when_a_burnout_mistake_costs_money()
        {
            var (engine, s) = NewGame(errorChance: 1.0);
            s.Life.Energy = 0; // exhausted: the burnout mistake is certain
            var r = engine.EndMonth(s, TestContent.Decisions(60, 10, 20, 10));
            Assert.IsTrue(r.BurnoutMistake);
            Assert.Greater(r.MistakeLoss.Cents, 0);
            Assert.AreEqual(r.NetProfit, r.GrossProfit - r.Fixed - r.Marketing - r.MistakeLoss - r.TaxReserve);
        }

        [Test]
        public void Decode_repairs_nested_state_that_would_break_the_month()
        {
            var (engine, s) = NewGame();
            var json = JObject.Parse(SaveCodec.Encode(s));
            json["LastDecisions"]["Work"] = 120;
            json["Facts"]["Items"] = null;
            json["Businesses"][0]["IndustryId"] = null;
            var loaded = SaveCodec.Decode(json.ToString()).State;
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.LastDecisions.TryGetAllocation(out _), "invalid saved decisions are replaced");
            Assert.IsNotNull(loaded.Facts.Items);
            Assert.IsTrue(loaded.Businesses.All(b => b.IndustryId != null));
            Assert.DoesNotThrow(() => engine.EndMonth(loaded, loaded.LastDecisions));
        }
    }
}
