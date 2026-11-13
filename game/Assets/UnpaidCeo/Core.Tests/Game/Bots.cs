using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Tests.Game
{
    /// <summary>A player strategy for balance simulations. Bots act only through GameEngine, so their games replay.</summary>
    internal interface IBot
    {
        MonthDecisions Decide(GameEngine e, GameState s);
        string PickChoice(GameEngine e, GameState s, StoryletDef scene);
        void AfterMonth(GameEngine e, GameState s, MonthReport report);
        void StartChapter2(GameEngine e, GameState s, string industry);
    }

    internal sealed class RunResult
    {
        /// <summary>Months played in chapters 1..3 (index = chapter; 0 when not finished).</summary>
        public readonly int[] ChapterMonths = new int[4];
        /// <summary>Weeks played in chapters 1..3 by <see cref="BotRunner.RunWeekly"/> (0 when not finished).</summary>
        public readonly int[] ChapterWeeks = new int[4];
        public bool Bankrupt;
        public bool Finished;
        public int ReachedChapter;
    }

    internal static class BotRunner
    {
        static ContentDb _db;

        /// <summary>The shipped content (all files).</summary>
        public static ContentDb Content()
        {
            if (_db != null) return _db;
            var dir = Path.Combine(UnityEngine.Application.dataPath, "UnpaidCeo", "Content");
            string Read(string f) { var p = Path.Combine(dir, f); return File.Exists(p) ? File.ReadAllText(p) : null; }
            return _db = ContentDb.FromJson(new ContentFiles
            {
                Industries = Read("industries.json"), Balance = Read("balance.json"), FreeActions = Read("free_actions.json"),
                Storylets = Read("storylets_ch1.json"), MoreStorylets = new[] { Read("storylets_ch2.json"), Read("storylets_ch3.json") },
                Npcs = Read("npcs.json"), Lessons = Read("lessons.json"), Missions = Read("missions.json"),
            });
        }

        public static RunResult Run(ContentDb db, ulong seed, string industry, IBot bot, int maxMonths = 400, int stopAfterChapter = 3)
        {
            var e = new GameEngine(db);
            var s = e.NewGame(seed, new HeroAppearance());
            e.TutorialGig(s);
            var result = new RunResult { ReachedChapter = 1 };
            int chapterStart = s.Month;
            while (s.Month < maxMonths)
            {
                foreach (var scene in e.BeginMonth(s).ToList())
                    e.ChooseStoryletOption(s, scene.Id, bot.PickChoice(e, s, scene));
                var report = e.EndMonth(s, bot.Decide(e, s));
                if (s.Facts.Has("bankrupt")) result.Bankrupt = true;
                bot.AfterMonth(e, s, report);
                if (!e.Chapter(s).GoalMet) continue;
                result.ChapterMonths[s.Chapter] = s.Month - chapterStart;
                if (s.Chapter >= stopAfterChapter) { result.Finished = true; break; }
                e.AdvanceChapter(s);
                result.ReachedChapter = s.Chapter;
                chapterStart = s.Month;
                if (s.Chapter == 2) bot.StartChapter2(e, s, industry);
            }
            return result;
        }

        /// <summary>The scene with the answers the hero cannot give yet removed (a knowledge lock).</summary>
        static StoryletDef Unlocked(StoryletDef st, GameState s)
        {
            var copy = new StoryletDef { Id = st.Id, YarnNode = st.YarnNode, Host = st.Host };
            foreach (var c in st.Choices) if (!HostScenes.Locked(st, c.Key, s)) copy.Choices[c.Key] = c.Value;
            return copy;
        }

        static string Open(string pick, StoryletDef st, GameState s) => HostScenes.Locked(st, pick, s) ? st.Choices.Keys.First(k => !HostScenes.Locked(st, k, s)) : pick;

        /// <summary>The career loop as the game plays it: a week per turn (BeginWeek, scenes, EndWeek).</summary>
        public static RunResult RunWeekly(ContentDb db, ulong seed, string industry, IBot bot, int maxWeeks = 1600, int stopAfterChapter = 1)
        {
            var e = new GameEngine(db);
            var s = e.NewGame(seed, new HeroAppearance());
            e.TutorialGig(s);
            var result = new RunResult { ReachedChapter = 1 };
            int chapterStart = s.Week;
            while (s.Week < maxWeeks)
            {
                foreach (var scene in e.BeginWeek(s).ToList())
                    e.ChooseStoryletOption(s, scene.Id, bot.PickChoice(e, s, scene));
                // stage 5B: scenes wait at their hosts; a player drops by every host each week
                foreach (var host in HostScenes.Hosts(db).ToList())
                {
                    var talk = e.TalkTo(s, host);
                    if (talk != null) e.ChooseStoryletOption(s, talk.Id, Open(bot.PickChoice(e, s, Unlocked(talk, s)), talk, s));
                }
                var report = e.EndWeek(s, bot.Decide(e, s));
                if (s.Facts.Has("bankrupt")) result.Bankrupt = true;
                bot.AfterMonth(e, s, report);
                if (!e.Chapter(s).GoalMet) continue;
                result.ChapterWeeks[s.Chapter] = s.Week - chapterStart;
                if (s.Chapter >= stopAfterChapter) { result.Finished = true; break; }
                e.AdvanceChapter(s);
                result.ReachedChapter = s.Chapter;
                chapterStart = s.Week;
                if (s.Chapter == 2) bot.StartChapter2(e, s, industry);
            }
            return result;
        }

        public static BusinessState Clone(BusinessState b) => new BusinessState
        {
            Id = b.Id, IndustryId = b.IndustryId, LotId = b.LotId, Price = b.Price, Units = b.Units,
            Marketing = b.Marketing, Employees = b.Employees, Receivables = new List<Money>(b.Receivables),
        };

        /// <summary>What this business would earn this month (operating profit) with the given settings.</summary>
        public static (Money op, int sold, int capacity) Forecast(GameEngine e, GameState s, BusinessState b, IndustryDef d, int work)
        {
            var bal = e.Content.Balance;
            double rep = 0.8 + 0.004 * s.Life.Reputation;
            double season = d.Seasonality != null && d.Seasonality.Length == 12 ? d.Seasonality[(s.Month - 1) % 12] : 1.0;
            double district = bal.DistrictDemand.TryGetValue(Chapters.District(s.Chapter), out var df) ? df : 1.0;
            double events = 1.0;
            foreach (var m in s.Modifiers) if (m.AppliesTo(d)) events *= m.Factor;
            double owner = Math.Min(1.0, work / (double)Math.Max(1, bal.Life.WorkSafe));
            var r = BusinessSimulator.Simulate(Clone(b), d, new BusinessInputs(rep, season, events, 0, district, bal.PayrollOverhead, owner));
            int capacity = (int)Math.Floor(d.CapacityPerUnit * Math.Max(0, b.Units) * BusinessSimulator.Staffing(b, d, owner) + 1e-9);
            return (r.Revenue - r.Cogs - r.Fixed - r.Marketing, r.UnitsSold, capacity);
        }

        public static BusinessState MainBusiness(GameEngine e, GameState s) =>
            s.Businesses.FirstOrDefault(b => e.Content.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == IndustryKind.Business);

        public static MonthDecisions Plan(GameState s, int work, int family, int rest, int study)
        {
            var d = new MonthDecisions { Work = work, Family = family, Rest = rest, Study = study };
            d.WorkSplit["gig_delivery"] = 100;
            return d;
        }

        /// <summary>
        /// Opens the first unit once cash (with a bank loan if needed) covers the capex plus <paramref name="months"/> of
        /// running costs: fixed costs, materials at full capacity and living costs. False while still saving up.
        /// </summary>
        /// <summary>Borrows what the industry screen suggests (GameEngine.FirstUnitBudget), so the optimal bot plays the UI's path.</summary>
        public static bool FundFirstUnit(GameEngine e, GameState s, IndustryDef d) => Fund(e, s, e.FirstUnitBudget(s, d));

        /// <summary>A thinner buffer: fixed and living costs for <paramref name="months"/> (the casual player borrows less than suggested).</summary>
        public static bool FundFirstUnit(GameEngine e, GameState s, IndustryDef d, int months)
        {
            var monthly = d.FixedMonthly + e.Content.Balance.PersonalCostsFor(s.Chapter).Total;
            return Fund(e, s, d.Capex + Money.FromCents(monthly.Cents * months + d.UnitCost.Cents * d.CapacityPerUnit * d.ReceivableDelayMonths));
        }

        static bool Fund(GameEngine e, GameState s, Money need)
        {
            if (s.Cash >= need) return true;
            var gap = need - s.Cash;
            if (gap > e.MaxLoan(s)) return false;
            return e.TakeLoan(s, Money.FromCents((gap.Cents + 9_999) / 10_000 * 10_000), 36);
        }

        static readonly Dictionary<string, int> NoNpcs = new Dictionary<string, int>();
    }

    /// <summary>
    /// Plays to win: best price and marketing for this month's operating profit, hires while a hire pays,
    /// expands when cash ≥ 1.5 × capex, borrows for the first unit, answers quizzes right, picks the best scene choice.
    /// </summary>
    internal sealed class OptimalBot : IBot
    {
        readonly bool _weekly;

        /// <param name="weekly">Plays weekly turns: spends the points the week can take and works the night shift too.</param>
        public OptimalBot(bool weekly = false) { _weekly = weekly; }

        public MonthDecisions Decide(GameEngine e, GameState s)
        {
            var bal = e.Content.Balance;
            // All-in on work, rest just enough to stay out of burnout; the rest action tops energy up.
            var d = BotRunner.Plan(s, 70, 5, 25, 0);
            int ap = _weekly ? ActionChoices.PointsTotal(s, e.Content) : LifeSimulator.ActionPoints(s.Life, 0, bal.Life);
            if (s.Chapter == 1 && s.Reserve < bal.PersonalCosts.Total && s.Cash.Cents > 20_000) d.FreeActions.Add("reserve_transfer");
            if (s.Life.Energy < 55 && ap-- >= 1) d.FreeActions.Add("rest");
            if (s.Life.Energy >= 65 && ap-- >= 1) d.FreeActions.Add("overtime");
            if (_weekly && s.Life.Energy >= 75 && ap-- >= 1) d.FreeActions.Add("night_shift");

            var b = BotRunner.MainBusiness(e, s);
            if (b == null) return d;
            var def = e.Content.Industries[b.IndustryId];
            var plan = BotRunner.Clone(b);
            if (plan.Units == 0)
            {
                if (!BotRunner.FundFirstUnit(e, s, def)) return d;   // keep gigging until it is safe
                plan.Units = 1;
            }
            else if (s.Cash.Cents >= def.Capex.Cents * 3 / 2 && plan.Units < 100)
            {
                var more = BotRunner.Clone(plan);
                more.Units++;
                bool helps = BotRunner.Forecast(e, s, more, def, d.Work).op > BotRunner.Forecast(e, s, plan, def, d.Work).op;
                if (helps || (s.Chapter == 3 && plan.Units < bal.Chapter3UnitsGoal)) plan.Units++;
            }
            OptimizePriceAndMarketing(e, s, plan, def, d.Work);
            // Staff: add while it pays (each step re-optimizes nothing; salary is in the forecast).
            int maxStaff = plan.Units * Math.Max(1, def.StaffPerUnit) * 2;
            for (int i = 0; i < maxStaff; i++)
            {
                var now = BotRunner.Forecast(e, s, plan, def, d.Work);
                if (now.sold < now.capacity || plan.Employees >= maxStaff) break;
                var hire = BotRunner.Clone(plan);
                hire.Employees++;
                if (BotRunner.Forecast(e, s, hire, def, d.Work).op <= now.op) break;
                plan.Employees++;
            }
            while (plan.Employees > 0)
            {
                var fire = BotRunner.Clone(plan);
                fire.Employees--;
                if (BotRunner.Forecast(e, s, fire, def, d.Work).op <= BotRunner.Forecast(e, s, plan, def, d.Work).op) break;
                plan.Employees--;
            }
            if (plan.Units != b.Units) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "units", ValueCents = plan.Units });
            if (plan.Price != b.Price) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "price", ValueCents = plan.Price.Cents });
            if (plan.Marketing != b.Marketing) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "marketing", ValueCents = plan.Marketing.Cents });
            if (plan.Employees != b.Employees) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "staff", ValueCents = plan.Employees });
            return d;
        }

        static void OptimizePriceAndMarketing(GameEngine e, GameState s, BusinessState plan, IndustryDef def, int work)
        {
            Money bestPrice = plan.Price, bestMarketing = plan.Marketing;
            long best = long.MinValue;
            foreach (var pct in Enumerable.Range(-6, 13).Select(i => 1.0 + i * 0.05))
            foreach (var mk in new[] { 0.0, 0.5, 1.0, 2.0 })
            {
                var trial = BotRunner.Clone(plan);
                trial.Price = def.RefPrice.MulRound(pct);
                trial.Marketing = def.MarketingK.MulRound(mk);
                long op = BotRunner.Forecast(e, s, trial, def, work).op.Cents;
                if (op > best) { best = op; bestPrice = trial.Price; bestMarketing = trial.Marketing; }
            }
            plan.Price = bestPrice;
            plan.Marketing = bestMarketing;
        }

        public string PickChoice(GameEngine e, GameState s, StoryletDef scene) =>
            scene.Choices.OrderByDescending(c => Score(c.Value)).ThenBy(c => c.Key, StringComparer.Ordinal).First().Key;

        static long Score(IEnumerable<Effect> effects)
        {
            long score = 0;
            foreach (var x in effects)
                switch (x)
                {
                    case CashEffect c: score += c.Amount.Cents; break;
                    case ReserveEffect r: score += r.Amount.Cents / 2; break;
                    case StatEffect st when st.Stat == "reputation": score += st.Delta * 2_000; break;
                    case StatEffect st when st.Stat == "energy": score += st.Delta * 500; break;
                    case DemandModifierEffect dm: score += (long)((dm.Factor - 1.0) * dm.Months * 300_000); break;
                    case StaffEffect sf: score += sf.Delta * 100_000; break;
                    case SetFactEffect f when f.FactId == "shark_debt" || f.FactId == "gray_scheme" || f.FactId == "sold_fakes": score -= 500_000; break;
                }
            return score;
        }

        public void AfterMonth(GameEngine e, GameState s, MonthReport report)
        {
            foreach (var id in report.Lessons)
                if (e.Content.Lessons.TryGetValue(id, out var lesson))
                    for (int q = 0; q < lesson.Quiz.Count; q++) e.AnswerQuiz(s, id, q, lesson.Quiz[q].CorrectIndex);
        }

        public void StartChapter2(GameEngine e, GameState s, string industry) => e.ChooseIndustry(s, industry);
    }

    /// <summary>
    /// Plays like a casual player: default prices, no marketing, the first scene choice, hires only after the hiring
    /// lesson (when the business is capacity-bound), opens a second unit only in chapter 3 with plenty of cash.
    /// </summary>
    internal sealed class CasualBot : IBot
    {
        /// <param name="weekly">Plays weekly turns (the same habits; the engine decides what a week can take).</param>
        public CasualBot(bool weekly = false) { }

        public MonthDecisions Decide(GameEngine e, GameState s)
        {
            var bal = e.Content.Balance;
            var d = BotRunner.Plan(s, 55, 15, 20, 10);   // the default split
            if (s.Chapter == 1 && s.Reserve < bal.PersonalCosts.Total && s.Cash.Cents > 40_000) d.FreeActions.Add("reserve_transfer");
            if (s.Life.Energy < 40) d.FreeActions.Add("rest");
            var b = BotRunner.MainBusiness(e, s);
            if (b == null) return d;
            var def = e.Content.Industries[b.IndustryId];
            int units = b.Units;
            if (units == 0 && BotRunner.FundFirstUnit(e, s, def, 1)) units = 1;
            if (s.Chapter == 3 && units < bal.Chapter3UnitsGoal && s.Cash.Cents >= def.Capex.Cents * 2) units++;
            if (units != b.Units) d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "units", ValueCents = units });
            if (s.LessonsSeen.ContainsKey("hiring") && b.Units > 0)
            {
                var f = BotRunner.Forecast(e, s, b, def, d.Work);
                double owner = Math.Min(1.0, d.Work / (double)Math.Max(1, bal.Life.WorkSafe));
                if (f.sold >= f.capacity && BusinessSimulator.Staffing(b, def, owner) <= 0.5)   // clearly understaffed
                    d.Levers.Add(new LeverChange { BusinessId = b.Id, Lever = "staff", ValueCents = b.Employees + 1 });
            }
            return d;
        }

        public string PickChoice(GameEngine e, GameState s, StoryletDef scene) =>
            scene.Choices.Keys.First();

        public void AfterMonth(GameEngine e, GameState s, MonthReport report) { }

        public void StartChapter2(GameEngine e, GameState s, string industry) => e.ChooseIndustry(s, industry);
    }
}
