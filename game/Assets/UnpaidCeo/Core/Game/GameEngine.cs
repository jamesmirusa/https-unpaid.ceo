using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Content;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Online;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    /// <summary>
    /// Runs the month loop: BeginMonth picks scenes, ChooseStoryletOption resolves them, EndMonth simulates.
    /// Every player action is appended to GameState.Log so Replay can rebuild the exact state.
    /// </summary>
    public sealed class GameEngine
    {
        const ulong RngSequence = 54;
        const double MistakeRevenueShare = 0.10;

        readonly ContentDb _c;

        public GameEngine(ContentDb content) { _c = content; }

        public ContentDb Content => _c;

        public GameState NewGame(ulong seed, HeroAppearance hero)
        {
            var s = new GameState
            {
                Seed = seed,
                Week = 1,
                Month = 1,
                Chapter = 1,
                Hero = hero ?? new HeroAppearance(),
                Rng = new Pcg32(seed, RngSequence).State,
                Life = new LifeStats { Energy = 70, Relationships = 60, Reputation = 50, Honesty = 70, Connections = 20 },
            };
            foreach (var id in _c.Balance.Chapter1Gigs)
            {
                if (!_c.Industries.TryGetValue(id, out var def)) continue;
                s.Businesses.Add(new BusinessState { Id = id, IndustryId = id, Price = def.Price });
            }
            s.LastDecisions = MonthDecisions.Default(s.Businesses.Count > 0 ? s.Businesses[0].Id : null);
            s.ActionPointsLeft = LifeSimulator.ActionPoints(s.Life, 0, _c.Balance.Life);
            return s;
        }

        /// <summary>
        /// A challenge or duel start: a new game in chapter 2 (Brooklyn) with the scenario's cash and its industry chosen,
        /// onboarding and the tutorial gig done. Nothing is logged: the RunStart rebuilds it.
        /// </summary>
        public GameState StartScenario(ScenarioDef d, HeroAppearance hero)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (d.IndustryId == null || !_c.Industries.TryGetValue(d.IndustryId, out var def) || def.Kind != IndustryKind.Business)
                throw new ArgumentException($"scenario industry '{d.IndustryId}' is not a business", nameof(d));
            var s = NewGame(d.Seed, hero);
            s.Chapter = 2;
            s.Cash = d.StartCash;
            foreach (var fact in new[] { "onboarding_done", "tutorial_gig_done", "chapter2_start", "industry_chosen" }) s.Facts.Set(fact, s.Month);
            s.Businesses.Add(new BusinessState { Id = d.IndustryId, IndustryId = d.IndustryId, Units = 0, Price = def.Price });
            return s;
        }

        /// <summary>The state a run begins from: a career's NewGame, or its scenario's start.</summary>
        public static GameState Start(ContentDb c, RunStart r, Func<RunStart, ScenarioDef> scenarioFor) =>
            StartFor(new GameEngine(c), r, scenarioFor);

        static GameState StartFor(GameEngine e, RunStart r, Func<RunStart, ScenarioDef> scenarioFor)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            if (r.Kind == RunStart.Career) return e.NewGame(r.Seed, r.Hero);
            if (r.Kind != RunStart.Challenge && r.Kind != RunStart.Duel) throw new ArgumentException($"unknown run kind '{r.Kind}'", nameof(r));
            var d = scenarioFor?.Invoke(r) ?? throw new ArgumentException($"no scenario for a {r.Kind} run", nameof(r));
            return e.StartScenario(d, r.Hero);
        }

        /// <summary>Onboarding gig: pays once, counts toward lifetime earnings.</summary>
        public void TutorialGig(GameState s)
        {
            if (s.Facts.Has("tutorial_gig_done")) return;
            s.Cash += _c.Balance.TutorialGigPay;
            s.LifetimeEarnings += _c.Balance.TutorialGigPay;
            s.Facts.Set("tutorial_gig_done", s.Month);
            Log(s, DecisionLogEntry.TutorialGig, null);
        }

        /// <summary>Picks this month's scenes (idempotent while scenes are pending).</summary>
        public IReadOnlyList<StoryletDef> BeginMonth(GameState s)
        {
            if (s.PendingStorylets.Count > 0) return s.PendingStorylets.Select(FindStorylet).ToList();
            // Scenes are rolled once per month: a relaunch after resolving them must not roll fresh ones.
            if (s.ScenesRolledMonth == s.Month) return Array.Empty<StoryletDef>();
            s.ScenesRolledMonth = s.Month;

            var rng = Pcg32.FromState(s.Rng);
            var picks = StoryletSelector.Pick(_c.Storylets, Context(s), s.StoryletLastMonth,
                s.StoryletsSeen.Keys.ToList(), rng, _c.Balance.EventsPerMonthWeights);
            s.Rng = rng.State;
            foreach (var p in picks)
            {
                s.StoryletLastMonth[p.Id] = s.Month;
                if (p.Once) s.StoryletsSeen[p.Id] = s.Month;
                s.PendingStorylets.Add(p.Id);
            }
            Log(s, DecisionLogEntry.BeginMonth, null);
            return picks;
        }

        /// <summary>Picks this week's scenes (idempotent while scenes are pending): about one a month (Balance.EventsPerWeekWeights).</summary>
        public IReadOnlyList<StoryletDef> BeginWeek(GameState s)
        {
            if (s.PendingStorylets.Count > 0) return s.PendingStorylets.Select(FindStorylet).ToList();
            if (s.ScenesRolledWeek == s.Week) return Array.Empty<StoryletDef>();
            s.ScenesRolledWeek = s.Week;

            var rng = Pcg32.FromState(s.Rng);
            var picks = StoryletSelector.Pick(_c.Storylets, Context(s), s.StoryletLastMonth,
                s.StoryletsSeen.Keys.ToList(), rng, _c.Balance.EventsPerWeekWeights);
            s.Rng = rng.State;
            foreach (var p in picks)
            {
                s.StoryletLastMonth[p.Id] = s.Month;
                if (p.Once) s.StoryletsSeen[p.Id] = s.Month;
                s.PendingStorylets.Add(p.Id);
            }
            Log(s, DecisionLogEntry.BeginWeek, null);
            return picks;
        }

        public void ChooseStoryletOption(GameState s, string storyletId, string choiceId)
        {
            if (!s.PendingStorylets.Contains(storyletId))
                throw new ArgumentException($"storylet '{storyletId}' is not pending", nameof(storyletId));
            var st = FindStorylet(storyletId);
            if (!st.Choices.TryGetValue(choiceId, out var effects))
                throw new ArgumentException($"storylet '{storyletId}' has no choice '{choiceId}'", nameof(choiceId));

            EffectApplier.ApplyAll(effects, s);
            s.PendingStorylets.Remove(storyletId);
            AddJournal(s, "decision", $"journal.{storyletId}.{choiceId}", ("storylet", storyletId), ("choice", choiceId));
            Log(s, DecisionLogEntry.StoryletChoice, new JObject { ["storylet"] = storyletId, ["choice"] = choiceId });
        }

        /// <summary>
        /// Checks a quiz answer for an unlocked lesson. Only the first answer to each question counts:
        /// it is recorded and logged, and earns the lesson's skill XP if correct. Later answers are checked but change nothing.
        /// </summary>
        public bool AnswerQuiz(GameState s, string lessonId, int question, int option)
        {
            if (lessonId == null || !_c.Lessons.TryGetValue(lessonId, out var lesson))
                throw new ArgumentException($"unknown lesson '{lessonId}'", nameof(lessonId));
            if (!s.LessonsSeen.ContainsKey(lessonId))
                throw new ArgumentException($"lesson '{lessonId}' is not unlocked", nameof(lessonId));
            if (question < 0 || question >= lesson.Quiz.Count)
                throw new ArgumentException($"lesson '{lessonId}' has no question {question}", nameof(question));
            var q = lesson.Quiz[question];
            if (option < 0 || option >= q.OptionKeys.Count)
                throw new ArgumentException($"question {question} of '{lessonId}' has no option {option}", nameof(option));

            bool correct = option == q.CorrectIndex;
            string key = lessonId + "/" + question;
            if (s.QuizAnswers.ContainsKey(key)) return correct;

            s.QuizAnswers[key] = option;
            if (correct)
            {
                var skill = lesson.SkillReward.Skill;
                s.Skills[skill] = (s.Skills.TryGetValue(skill, out var xp) ? xp : 0) + lesson.SkillReward.Xp;
            }
            Log(s, DecisionLogEntry.QuizAnswer, new JObject { ["lesson"] = lessonId, ["question"] = question, ["option"] = option });
            return correct;
        }

        /// <summary>How much more the bank lends: a base amount plus reputation, minus what is still owed.</summary>
        public Money MaxLoan(GameState s)
        {
            var l = _c.Balance.Loans;
            var max = l.MaxBase + Money.FromCents(l.MaxPerReputation.Cents * Math.Max(0, s.Life.Reputation));
            foreach (var loan in s.Loans) max -= loan.Balance;
            return max.Cents < 0 ? Money.Zero : max;
        }

        /// <summary>Base APR, plus a premium for reputation below 50 and for low honesty.</summary>
        public double LoanApr(GameState s)
        {
            var l = _c.Balance.Loans;
            return l.BaseApr + Math.Max(0, 50 - s.Life.Reputation) * l.AprPerMissingReputation
                + (s.Life.Honesty < l.LowHonestyBelow ? l.LowHonestyAprAdd : 0.0);
        }

        /// <summary>Takes a bank loan: cash now, annuity payments from the end of this month.</summary>
        public bool TakeLoan(GameState s, Money principal, int months)
        {
            if (s.Chapter < 2 || principal.Cents <= 0 || principal > MaxLoan(s) || Array.IndexOf(_c.Balance.Loans.TermsMonths, months) < 0) return false;
            double apr = LoanApr(s);
            s.Loans.Add(new LoanState
            {
                Id = $"loan_{s.Month}_{s.Loans.Count}",
                Principal = principal,
                Balance = principal,
                Apr = apr,
                MonthsLeft = months,
                Payment = EconomyFormulas.LoanPayment(principal, apr, months),
            });
            s.Cash += principal;
            if (!s.Facts.Has("first_loan")) s.Facts.Set("first_loan", s.Month);
            AddJournal(s, "decision", "journal.take_loan", ("amount", principal.Cents.ToString()), ("months", months.ToString()));
            Log(s, DecisionLogEntry.TakeLoan, new JObject { ["principal"] = principal.Cents, ["months"] = months });
            return true;
        }

        /// <summary>Pays every loan's monthly instalment; returns (payments, interest).</summary>
        static (Money payments, Money interest) PayLoans(GameState s)
        {
            Money payments = Money.Zero, interest = Money.Zero;
            foreach (var loan in s.Loans)
            {
                var i = loan.Balance.MulRound(loan.Apr / 12.0);
                var pay = loan.MonthsLeft <= 1 ? loan.Balance + i : Money.FromCents(Math.Min(loan.Payment.Cents, (loan.Balance + i).Cents));
                loan.Balance -= pay - i;
                loan.MonthsLeft--;
                payments += pay;
                interest += i;
            }
            s.Loans.RemoveAll(l => l.Balance.Cents <= 0 || l.MonthsLeft <= 0);
            return (payments, interest);
        }

        public ChapterStatus Chapter(GameState s) => new ChapterStatus(s.Chapter, Chapters.GoalMet(s, _c), Chapters.Progress(s, _c));

        /// <summary>Picks the chapter-2 business (once): it starts with no units, at its reference price.</summary>
        public bool ChooseIndustry(GameState s, string industryId)
        {
            if (s.Chapter < 2 || industryId == null || !_c.Balance.Chapter2Industries.Contains(industryId)
                || !_c.Industries.TryGetValue(industryId, out var def) || def.Kind != IndustryKind.Business) return false;
            if (s.Businesses.Any(b => _c.Industries.TryGetValue(b.IndustryId, out var d) && d.Kind == IndustryKind.Business)) return false;
            s.Businesses.Add(new BusinessState { Id = industryId, IndustryId = industryId, Units = 0, Price = def.Price });
            SetOnce(s, "industry_chosen");
            AddJournal(s, "decision", "journal.choose_industry", ("industry", industryId));
            Log(s, DecisionLogEntry.ChooseIndustry, new JObject { ["industry"] = industryId });
            return true;
        }

        /// <summary>Moves to the next chapter (and district) once the current goal is met. Chapter 3 is the last one in this stage.</summary>
        /// <summary>
        /// Cash to open the first unit safely: capex, fixed and living costs for 2 + payment-delay months, and the materials
        /// B2B clients have not paid for yet.
        /// </summary>
        public Money FirstUnitBudget(GameState s, IndustryDef d)
        {
            int months = 2 + Math.Max(0, d.ReceivableDelayMonths);
            var monthly = d.FixedMonthly + _c.Balance.PersonalCostsFor(s.Chapter).Total;
            return d.Capex + Money.FromCents(monthly.Cents * months + d.UnitCost.Cents * d.CapacityPerUnit * Math.Max(0, d.ReceivableDelayMonths));
        }

        /// <summary>The loan the industry screen suggests: the first-unit budget minus cash, rounded up to $100, within the limit.</summary>
        public Money SuggestedFirstUnitLoan(GameState s, string industryId)
        {
            var gap = FirstUnitBudget(s, _c.Industries[industryId]) - s.Cash;
            if (gap.Cents <= 0) return Money.FromCents(0);
            long rounded = (gap.Cents + 9_999) / 10_000 * 10_000;
            return Money.FromCents(Math.Min(rounded, Math.Max(0, MaxLoan(s).Cents)));
        }

        public bool AdvanceChapter(GameState s)
        {
            if (s.Chapter >= Chapters.LastChapter || !Chapters.GoalMet(s, _c)) return false;
            RecordChapterDone(s);   // the goal may have been met mid-month, before the month-end check
            s.Chapter++;
            SetOnce(s, $"chapter{s.Chapter}_start");
            AddJournal(s, "decision", $"journal.chapter{s.Chapter}_start", ("district", s.District));
            Log(s, DecisionLogEntry.AdvanceChapter, null);
            return true;
        }

        /// <summary>Legacy turn (stage 1–3 logs): a whole month at once. New games play <see cref="EndWeek"/>.</summary>
        public MonthReport EndMonth(GameState s, MonthDecisions d) => Settle(s, d, weekly: false);

        /// <summary>
        /// Stage 4B turn: one week. Gig pay and running costs are a quarter of the month's; rent falls on week 1,
        /// essentials on week 2, loans on week 4; the month's action points are a pool spent across its weeks.
        /// </summary>
        public MonthReport EndWeek(GameState s, MonthDecisions d) => Settle(s, d, weekly: true);

        MonthReport Settle(GameState s, MonthDecisions d, bool weekly)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            if (!d.TryGetAllocation(out var time))
                throw new ArgumentException("time allocation must be four non-negative parts summing to 100", nameof(d));

            var b = _c.Balance;
            int wk = weekly ? s.WeekOfMonth : 1;
            bool monthStart = !weekly || wk == 1, monthEnd = !weekly || wk == GameState.WeeksPerMonth;
            double share = weekly ? 1.0 / GameState.WeeksPerMonth : 1.0;
            var rng = Pcg32.FromState(s.Rng);
            var report = new MonthReport { Month = s.Month, Week = s.Week, WeekOfMonth = wk, IsWeek = weekly, CashStart = s.Cash };
            report.Storylets.AddRange(s.StoryletLastMonth.Where(kv => kv.Value == s.Month).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal));

            ApplyLevers(s, d, report);

            // Free actions spend the month's pool of action points, refilled on its first week; each action at most
            // once a month, an action that opens a fact (a debt, a scheme) waits until that fact is resolved, and an
            // action needs the money and the energy it takes (ActionChoices.Check: the buttons close the same ones).
            if (monthStart)
            {
                s.ActionPointsLeft = LifeSimulator.ActionPoints(s.Life, 0, b.Life);
                s.ActionsThisMonth.Clear();
            }
            double errorChance = LifeSimulator.ErrorChance(s.Life, b.Life);
            foreach (var id in d.FreeActions)
            {
                if (!_c.FreeActions.TryGetValue(id, out var fa)
                    || ActionChoices.Check(fa, s.ActionPointsLeft, s.Life.Energy, s.Cash, s.ActionsThisMonth, s.Facts) != ChoiceState.Open)
                { report.RejectedActions.Add(id); continue; }
                s.ActionPointsLeft -= fa.ActionPoints;
                s.ActionsThisMonth.Add(id);
                s.Cash -= fa.Cost;
                EffectApplier.ApplyAll(fa.Effects, s);
                AddJournal(s, "decision", fa.JournalKey ?? $"journal.{id}", ("action", id));
            }

            report.Life = LifeSimulator.Apply(s.Life, time, b.Life, share);
            s.StudyXp += report.Life.StudyXp;

            // Businesses and gigs.
            var repFactor = 0.8 + 0.004 * s.Life.Reputation;
            // Splits are shares of work time; totals above 100 are scaled down instead of multiplying capacity.
            int splitTotal = Math.Max(100, d.WorkSplit.Values.Where(v => v > 0).Sum());
            Money revenue = Money.Zero, cogs = Money.Zero, fixedCosts = Money.Zero, marketing = Money.Zero, collected = Money.Zero;
            // Once a business has a unit, the owner works it: gigs stop paying, and the owner counts as
            // a staff member in proportion to their work time (a full-time load is LifeBalance.WorkSafe).
            bool ownsUnits = s.Businesses.Any(x => x.Units > 0 && _c.Industries.TryGetValue(x.IndustryId, out var xd) && xd.Kind == IndustryKind.Business);
            double ownerShare = Math.Min(1.0, time.Work / (double)Math.Max(1, b.Life.WorkSafe));
            foreach (var bs in s.Businesses)
            {
                if (!_c.Industries.TryGetValue(bs.IndustryId, out var def)) continue;
                int share0 = 0;
                if (def.Kind == IndustryKind.Gig && !ownsUnits && s.StudyXp >= def.RequiredStudy && d.WorkSplit.TryGetValue(bs.Id, out var split))
                    share0 = (int)Math.Round(time.Work * Math.Max(0, split) / (double)splitTotal, MidpointRounding.AwayFromZero);
                double season = def.Seasonality != null && def.Seasonality.Length == 12 ? def.Seasonality[(s.Month - 1) % 12] : 1.0;
                double district = b.DistrictDemand.TryGetValue(Chapters.District(s.Chapter), out var df) ? df : 1.0;
                double events = 1.0;
                foreach (var m in s.Modifiers) if (m.AppliesTo(def)) events *= m.Factor;
                var r = BusinessSimulator.Simulate(bs, def, new BusinessInputs(repFactor, season, events, share0, district, b.PayrollOverhead, ownerShare),
                    share, weekly ? def.ReceivableDelayMonths * GameState.WeeksPerMonth : -1);
                report.BusinessResults[bs.Id] = r;
                if (def.Kind == IndustryKind.Business && bs.Units >= 1 && (r.Revenue - r.Cogs - r.Fixed - r.Marketing).Cents >= 0)
                    SetOnce(s, "break_even");
                revenue += r.Revenue; cogs += r.Cogs; fixedCosts += r.Fixed; marketing += r.Marketing; collected += r.CashCollected;
            }

            var mistakeLoss = Money.Zero;
            if (rng.NextDouble() < errorChance && revenue.Cents > 0)
            {
                mistakeLoss = revenue.MulRound(MistakeRevenueShare);
                report.BurnoutMistake = true;
                AddJournal(s, "consequence", "journal.burnout_mistake", ("loss", mistakeLoss.Cents.ToString()));
            }

            report.Revenue = revenue;
            report.Cogs = cogs;
            report.GrossProfit = revenue - cogs;
            report.Fixed = fixedCosts;
            report.Marketing = marketing;
            report.MistakeLoss = mistakeLoss;
            report.OperatingProfit = report.GrossProfit - fixedCosts - marketing - mistakeLoss;
            (report.LoanPayments, report.Interest) = monthEnd ? PayLoans(s) : (Money.Zero, Money.Zero);
            if (report.LoanPayments.Cents > 0) report.Paid.Add(new LineItem("loan.pay", report.LoanPayments));
            report.TaxReserve = EconomyFormulas.TaxReserve(report.OperatingProfit - report.Interest, b.TaxRate);
            report.NetProfit = report.OperatingProfit - report.Interest - report.TaxReserve;

            s.LifetimeEarnings += revenue;
            bool bills = s.LifetimeEarnings >= b.BillsStartAfter;
            var costs = b.PersonalCostsFor(s.Chapter);
            if (bills)
            {
                if (weekly) { foreach (var item in costs.ForWeek(wk)) { report.Paid.Add(item); report.PersonalCosts += item.Amount; } }
                else
                {
                    report.PersonalCosts = costs.Total;
                    report.Paid.Add(new LineItem("bill.room", costs.Room)); report.Paid.Add(new LineItem("bill.food", costs.Food));
                    report.Paid.Add(new LineItem("bill.transit", costs.Transit)); report.Paid.Add(new LineItem("bill.essentials", costs.Essentials));
                }
            }

            s.Cash += collected - cogs - fixedCosts - marketing - mistakeLoss - report.TaxReserve - report.PersonalCosts - report.LoanPayments;
            report.CashEnd = s.Cash;

            // What next week brings, so the player can plan for it.
            if (weekly)
            {
                int next = wk % GameState.WeeksPerMonth + 1;
                if (bills) report.DueNext.AddRange(costs.ForWeek(next));
                if (next == GameState.WeeksPerMonth && s.Loans.Count > 0)
                {
                    var due = Money.Zero;
                    foreach (var loan in s.Loans) due += loan.MonthsLeft <= 1 ? loan.Balance + loan.Balance.MulRound(loan.Apr / 12.0) : loan.Payment;
                    report.DueNext.Add(new LineItem("loan.pay", due));
                }
            }

            UpdateConsequenceFacts(s, report, b, monthEnd);
            QueueTriggeredLessons(s);

            // Each lesson reaches the player once, whether a fact or a storylet effect queued it.
            foreach (var id in s.PendingLessons)
            {
                if (s.LessonsSeen.ContainsKey(id)) continue;
                s.LessonsSeen[id] = s.Month;
                report.Lessons.Add(id);
            }
            s.PendingLessons.Clear();
            s.PendingStorylets.Clear();
            if (monthEnd)
            {
                foreach (var m in s.Modifiers) m.MonthsLeft--;
                s.Modifiers.RemoveAll(m => m.MonthsLeft <= 0);
            }
            s.LastDecisions = d;
            Log(s, weekly ? DecisionLogEntry.EndWeek : DecisionLogEntry.EndMonth, JObject.FromObject(d));
            s.Rng = rng.State;
            s.Week += weekly ? 1 : GameState.WeeksPerMonth;
            s.Month = GameState.MonthOf(s.Week);
            return report;
        }

        public static GameState Replay(ContentDb content, ulong seed, HeroAppearance hero, IEnumerable<DecisionLogEntry> log)
        {
            var e = new GameEngine(content);
            var s = e.NewGame(seed, hero);
            MonthReport last = null;
            foreach (var entry in log.ToList()) Apply(e, s, entry, ref last);
            return s;
        }

        /// <summary>
        /// Server-side replay: every logged action must succeed exactly as it did when it was logged. Stops at the first
        /// entry that is refused (<c>refused:&lt;kind&gt;</c>) or unreadable (<c>bad_payload…</c>).
        /// </summary>
        /// <param name="beforeEntry">Sees the state before each entry is applied (the server's pacing snapshot).</param>
        public static ReplayResult ReplayStrict(ContentDb c, RunStart start, Func<RunStart, ScenarioDef> scenarioFor,
            IReadOnlyList<DecisionLogEntry> log, Action<GameState, DecisionLogEntry> beforeEntry = null)
        {
            var e = new GameEngine(c);
            GameState s;
            try { s = StartFor(e, start, scenarioFor); }
            catch (ArgumentException ex) { return new ReplayResult { FailedAt = 0, Error = "bad_start:" + ex.Message }; }
            bool scenario = start.Kind != RunStart.Career;
            MonthReport last = null;
            for (int i = 0; i < (log?.Count ?? 0); i++)
            {
                var entry = log[i];
                try
                {
                    if (entry == null) return Fail(i, "bad_payload:null entry");
                    if (scenario && !ScenarioActions.Contains(entry.Kind)) return Fail(i, "refused:" + entry.Kind);
                    beforeEntry?.Invoke(s, entry);
                    if (!Apply(e, s, entry, ref last)) return Fail(i, "refused:" + entry.Kind);
                }
                catch (JsonException ex) { return Fail(i, "bad_payload:" + ex.GetType().Name); }
                catch (FormatException ex) { return Fail(i, "bad_payload:" + ex.Message); }
                catch (InvalidCastException) { return Fail(i, "bad_payload:type"); }
                catch (NullReferenceException) { return Fail(i, "bad_payload:missing field"); }
                catch (ArgumentNullException) { return Fail(i, "bad_payload:missing field"); }
                catch (ArgumentException) { return Fail(i, "refused:" + entry.Kind); }
                catch (InvalidOperationException) { return Fail(i, "refused:" + entry.Kind); }
            }
            return new ReplayResult { State = s, LastReport = last };

            ReplayResult Fail(int at, string error) => new ReplayResult { State = s, LastReport = last, FailedAt = at, Error = error };
        }

        /// <summary>
        /// What a challenge or duel run may log: the client's RunController plays month plans and loans only — no scenes,
        /// quizzes or chapter moves, so every player of a scenario meets the same market.
        /// </summary>
        static readonly HashSet<string> ScenarioActions = new HashSet<string> { DecisionLogEntry.EndMonth, DecisionLogEntry.EndWeek, DecisionLogEntry.TakeLoan };

        /// <summary>Applies one logged action; false when the engine refuses it (a strict replay rejects the log).</summary>
        static bool Apply(GameEngine e, GameState s, DecisionLogEntry entry, ref MonthReport last)
        {
            switch (entry.Kind)
            {
                case DecisionLogEntry.TutorialGig:
                    if (s.Facts.Has("tutorial_gig_done")) return false;
                    e.TutorialGig(s);
                    return true;
                case DecisionLogEntry.BeginMonth: e.BeginMonth(s); return true;
                case DecisionLogEntry.BeginWeek: e.BeginWeek(s); return true;
                case DecisionLogEntry.EndWeek:
                    var wd = JsonConvert.DeserializeObject<MonthDecisions>(entry.Payload) ?? throw new FormatException("empty endWeek");
                    last = e.EndWeek(s, wd);
                    return true;
                case DecisionLogEntry.StoryletChoice:
                    var p = JObject.Parse(entry.Payload);
                    e.ChooseStoryletOption(s, (string)p["storylet"], (string)p["choice"]);
                    return true;
                case DecisionLogEntry.EndMonth:
                    var d = JsonConvert.DeserializeObject<MonthDecisions>(entry.Payload) ?? throw new FormatException("empty endMonth");
                    last = e.EndMonth(s, d);
                    return true;
                case DecisionLogEntry.TakeLoan:
                    var l = JObject.Parse(entry.Payload);
                    return e.TakeLoan(s, Money.FromCents((long)l["principal"]), (int)l["months"]);
                case DecisionLogEntry.ChooseIndustry:
                    return e.ChooseIndustry(s, (string)JObject.Parse(entry.Payload)["industry"]);
                case DecisionLogEntry.AdvanceChapter: return e.AdvanceChapter(s);
                case DecisionLogEntry.QuizAnswer:
                    var q = JObject.Parse(entry.Payload);
                    return e.AnswerQuiz(s, (string)q["lesson"], (int)q["question"], (int)q["option"]);
                default: throw new FormatException($"unknown log entry kind '{entry.Kind}'");
            }
        }

        const int MaxUnits = 100;

        void ApplyLevers(GameState s, MonthDecisions d, MonthReport report)
        {
            foreach (var lever in d.Levers)
            {
                var bs = s.Businesses.FirstOrDefault(x => x.Id == lever.BusinessId);
                if (bs == null || lever.ValueCents < 0) continue;
                _c.Industries.TryGetValue(bs.IndustryId, out var def);
                bool business = def != null && def.Kind == IndustryKind.Business;
                switch (lever.Lever)
                {
                    case "price":
                        bs.Price = Money.FromCents(lever.ValueCents);
                        if (business && bs.Price != def.Price) SetOnce(s, "price_changed");
                        break;
                    case "marketing":
                        bs.Marketing = Money.FromCents(lever.ValueCents);
                        if (business && bs.Marketing.Cents > 0) SetOnce(s, "marketing_started");
                        break;
                    case "units":
                        if (business) SetUnits(s, bs, def, (int)Math.Min(lever.ValueCents, MaxUnits), report);
                        break;
                    case "staff":
                        if (!business) break;
                        int target = (int)Math.Min(lever.ValueCents, MaxStaff(bs, def));
                        if (target > bs.Employees) SetOnce(s, "first_hire");
                        bs.Employees = target;
                        break;
                }
            }
        }

        static int MaxStaff(BusinessState bs, IndustryDef def) => Math.Max(0, bs.Units) * Math.Max(1, def.StaffPerUnit) * 2;

        void SetUnits(GameState s, BusinessState bs, IndustryDef def, int target, MonthReport report)
        {
            if (target > bs.Units)
            {
                var cost = Money.FromCents(def.Capex.Cents * (target - bs.Units));
                if (s.Cash < cost) { report.RejectedLevers.Add($"units:{bs.Id}"); return; }
                s.Cash -= cost;
                report.Capex += cost;
                bs.Units = target;
                if (bs.Units >= 2) SetOnce(s, "second_unit");
            }
            else if (target < bs.Units)
            {
                var refund = Money.FromCents(def.Capex.MulRound(_c.Balance.AssetResaleRate).Cents * (bs.Units - target));
                s.Cash += refund;
                report.AssetSales += refund;
                bs.Units = target;
                bs.Employees = Math.Min(bs.Employees, MaxStaff(bs, def));
            }
        }

        static void SetOnce(GameState s, string fact)
        {
            if (!s.Facts.Has(fact)) s.Facts.Set(fact, s.Month);
        }

        void QueueTriggeredLessons(GameState s)
        {
            foreach (var l in _c.LessonsInOrder)
                if (s.Facts.Has(l.TriggerFact) && !s.LessonsSeen.ContainsKey(l.Id) && !s.PendingLessons.Contains(l.Id))
                    s.PendingLessons.Add(l.Id);
        }

        void UpdateConsequenceFacts(GameState s, MonthReport r, Balance b, bool monthEnd = true)
        {
            if (r.Revenue.Cents > 0 && !s.Facts.Has("first_income"))
            {
                s.Facts.Set("first_income", s.Month);
                AddJournal(s, "consequence", "journal.first_income");
            }

            if (r.PersonalCosts.Cents > 0 && !s.Facts.Has("first_bills"))
            {
                s.Facts.Set("first_bills", s.Month);
                AddJournal(s, "consequence", "journal.first_bills");
            }

            if (s.Businesses.Any(x => x.Employees > 0)) SetOnce(s, "first_payroll");

            // Buying or selling units moves cash but is an investment, not a profit or loss.
            // Repaying loan principal is financing, not a loss either.
            var operatingCashEnd = r.CashEnd + r.Capex - r.AssetSales + (r.LoanPayments - r.Interest);
            if ((r.NetProfit.Cents < 0 || operatingCashEnd.Cents < r.CashStart.Cents) && !s.Facts.Has("first_loss"))
            {
                s.Facts.Set("first_loss", s.Month);
                AddJournal(s, "consequence", "journal.first_loss");
            }

            // Profitable on paper, short of cash: clients still owe money while the account is negative (B2B lesson).
            if (s.Cash.Cents < 0 && s.Businesses.Any(x => x.Receivables != null && x.Receivables.Any(m => m.Cents > 0)))
                SetOnce(s, "receivable_gap");

            if (s.Cash.Cents < 0)
            {
                if (!s.Facts.Has("in_debt")) AddJournal(s, "consequence", "journal.in_debt");
                s.Facts.Set("in_debt", s.Facts.MonthSet("in_debt") ?? s.Month);
            }
            else s.Facts.Clear("in_debt");

            if (monthEnd)
            {
                // The comeback clears "bankrupt": restart the count so the same month cannot re-bankrupt the player.
                if (!s.Facts.Has("bankrupt") && s.MonthsInDeepDebt >= 2) s.MonthsInDeepDebt = 0;
                s.MonthsInDeepDebt = s.Cash < b.DebtBankruptLine ? s.MonthsInDeepDebt + 1 : 0;
                if (s.MonthsInDeepDebt >= 2 && !s.Facts.Has("bankrupt"))
                {
                    s.Facts.Set("bankrupt", s.Month);
                    AddJournal(s, "consequence", "journal.bankrupt");
                }
            }
            if (s.Cash.Cents >= 0) s.Facts.Clear("bankrupt");

            // 6: the chapter-3 team counts only when it is kept from one month end to the next.
            if (monthEnd) s.TeamMonths = s.Businesses.Sum(x => x.Employees) >= b.Chapter3EmployeesGoal ? s.TeamMonths + 1 : 0;
            RecordChapterDone(s);
        }

        /// <summary>Sets chapterN_done (and its journal line) once the current chapter's goal is met.</summary>
        void RecordChapterDone(GameState s)
        {
            if (s.Chapter < 1 || s.Chapter > Chapters.LastChapter) return;
            string fact = $"chapter{s.Chapter}_done";
            if (s.Facts.Has(fact) || !Chapters.GoalMet(s, _c)) return;
            s.Facts.Set(fact, s.Month);
            AddJournal(s, "consequence", "journal." + fact);
        }

        NarrativeContext Context(GameState s) => new NarrativeContext
        {
            Month = s.Month,
            Chapter = s.Chapter,
            Cash = s.Cash,
            Life = s.Life,
            Facts = s.Facts,
            Npcs = s.Npcs,
            IndustryIds = s.Businesses.Select(x => x.IndustryId).ToList(),
        };

        StoryletDef FindStorylet(string id) =>
            _c.Storylets.FirstOrDefault(x => x.Id == id) ?? throw new ArgumentException($"unknown storylet '{id}'", nameof(id));

        static void AddJournal(GameState s, string kind, string key, params (string k, string v)[] args)
        {
            var e = new JournalEntry { Month = s.Month, Kind = kind, TextKey = key };
            foreach (var (k, v) in args) e.Args[k] = v;
            s.Journal.Add(e);
        }

        static void Log(GameState s, string kind, JToken payload) =>
            s.Log.Add(new DecisionLogEntry { Month = s.Month, Kind = kind, Payload = payload == null ? null : CanonicalJson.Serialize(payload) });
    }
}
