# Stage 2A — Chapters 2–3, six industries, pacing — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** After chapter 1 the player moves to Brooklyn, picks one of six industries, builds it to break-even and $25k (chapter 2), then grows it in Queens to a second location and ten employees or $250k (chapter 3) — on a daily pace that makes the whole game last a month of real time.

**Architecture:** Everything rule-like lands in the pure C# core (`UnpaidCeo.Core`): industry data, staffing, loans, demand modifiers, chapter goals, net worth, a daily turn budget — each new player action is a logged, replayable decision. Unity adds two Addressable districts, new screens and the art/voice batches (each behind an owner-approved estimate).

**Tech Stack:** Unity 6000.6.2f1, URP, UI Toolkit, Unity Localization, Addressables, Yarn Spinner 3.2.8, Newtonsoft.Json; Higgsfield (GPT Image 2.6, Tripo P2/H3.1), ElevenLabs eleven_v4.

**Spec:** `docs/superpowers/specs/2026-11-08-unpaid-ceo-2-design.md` (§3.1–3.5, 3a, 5.2, 7.6, 11). Stage-1 rulings: `docs/superpowers/rulings/2026-11-08-stage1-rulings.md`.

**Sibling plans (written after this one):** 2B online and multiplayer (UGS auth, cloud save, leagues, weekly challenge, friends and async duels, market wars, analytics) · 2C 17 languages · 2D payments (Stripe, Telegram Stars) · 2E launch (landing, Telegram Mini App, unpaid.ceo).

## Global Constraints

- Money is integer cents (`Money`); the core is deterministic: seed + decision log → identical `GameState` (hash). Every new player action is logged and handled by `GameEngine.Replay`.
- Content is data (JSON / Yarn); unknown keys throw at load (stage-1 rule).
- Voice in EN and RU only; other languages get subtitles (plan 2C).
- Before any Higgsfield or ElevenLabs generation, show the owner an estimate. Owner rule (2026-11-08): maximum quality at the best price; when the quality gap is large, take the better model.
- Web `Build/*` ≤ 20 MiB (`tools/check-build-size.sh`); districts stream as Addressables.
- Buildings 2–8k triangles at LOD0, characters 15–22k; draw calls Eco ≤ 100, Optimal ≤ 250, Max ≤ 600.
- The industry screen shows «Деньги в игре симулированные, это не финансовый совет» / "Money in the game is simulated; this is not financial advice."
- Chapters 1–3 are free. No pay-to-skip: money cannot buy turns or capital.
- Pacing (owner, 2026-11-08): the whole game (chapters 1–5) takes a skilled player at least 30 real days; difficulty comes from decisions, not grind.

## Pacing decisions (binding for Tasks 8 and 15)

- Daily turn budget: 6 manual months per local calendar day, plus offline autopilot (spec §3.1: 1 month per 4 h, max 2) → at most 8 months a day.
- Target months for an optimal bot: chapter 1 ≥ 10, chapter 2 ≥ 24, chapter 3 ≥ 36 (free chapters ≥ 70 months ≈ 9+ days). Chapters 4–5 (stage 4) are tuned to ≥ 150 months, giving ≥ 220 months ≈ 28–30 days.
- A casual bot still finishes chapter 2 within 60 months with every industry (hard, not impossible).
- When turns run out the game offers things to do: Academy and quizzes, daily missions, home; the weekly challenge and duels come with plan 2B.

## Review Focus

1. A player reaches chapter 2 with too little cash for any capex — they can still start (cheapest capex ≤ chapter-1 goal, or a bank loan covers it). Test: Task 6 `Every_industry_can_be_started_from_the_chapter1_goal`.
2. A stage-1 save (version 1, chapter 1) loads and continues; new fields default. Test: Task 6 `Version1_save_loads_as_version2`.
3. Cash goes negative while loan payments are due — the in-debt/bankruptcy arc still works, the loan is not dropped, large sums ($250k+) stay exact. Test: Task 4 `Payments_continue_when_cash_is_negative`.
4. A log containing every new action kind replays to the same state hash (server validation in 2B depends on it). Test: Task 6 `Replay_reproduces_every_action_kind`.
5. Device clock set back, or a time-zone change, never grants extra turns. Test: Task 8 `Clock_set_back_grants_nothing`.

---

### Task 1: Industry and balance data for chapters 2–3

**Files:**
- Modify: `game/Assets/UnpaidCeo/Core/Economy/IndustryDef.cs`
- Modify: `game/Assets/UnpaidCeo/Core/Content/Balance.cs`, `Core/Content/ContentValidator.cs`
- Modify: `game/Assets/UnpaidCeo/Content/industries.json`, `Content/balance.json`
- Test: `game/Assets/UnpaidCeo/Core.Tests/Content/IndustryContentTests.cs`

**Interfaces:**
- Produces on `IndustryDef`: `int StaffPerUnit`, `Money Salary`, `int Chapter` (gigs 1, businesses 2), `double RepeatPurchases`, `double[] Seasonality` (12 values, default all 1.0), `List<string> ScaleNameKeys` (3 keys: 1 unit / 2–4 / 5+), `string LessonId`.
- Produces on `Balance`: `Dictionary<string,double> DistrictDemand` (bronx 1.0, brooklyn 1.0, queens 1.15), `Money Chapter2CapitalGoal` (2_500_000), `Money Chapter3CapitalGoal` (25_000_000), `int Chapter3EmployeesGoal` (10), `int Chapter3UnitsGoal` (2), `double AssetResaleRate` (0.5), `double PayrollOverhead` (0.10), `LoanBalance Loans` { `double BaseApr` 0.12, `double AprPerMissingReputation` 0.001, `double LowHonestyAprAdd` 0.03, `int LowHonestyBelow` 40, `Money MaxBase` 1_000_000, `Money MaxPerReputation` 50_000, `int[] TermsMonths` {12, 24, 36} }, `int DailyManualMonths` (6), `List<string> Chapter2Industries`.

Starting values (cents; game assumptions, recorded in `balance.sources` with a "game assumption — tuned by BalanceSimulationTests" note; Task 15 may change them):

| id | capex | price | unitCost | cap/unit | fixed/unit | staff | salary | baseDemand | elast. | mA | mK | delay | repeat | seasonality (Jan…Dec) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| coffee_cart | 600000 | 400 | 120 | 3000 | 120000 | 1 | 280000 | 2200 | 1.4 | 0.35 | 50000 | 0 | 20 | 1.15,1.15,1.05,1,0.95,0.9,0.9,0.9,1,1.05,1.1,1.15 |
| resale_store | 300000 | 4500 | 2200 | 250 | 60000 | 1 | 260000 | 220 | 1.8 | 0.5 | 30000 | 0 | 3 | 0.9,0.85,0.95,1,1,0.95,0.95,1,1,1.05,1.3,1.4 |
| food_truck | 2500000 | 1200 | 400 | 2500 | 350000 | 2 | 300000 | 1800 | 1.2 | 0.3 | 50000 | 0 | 6 | 0.6,0.6,0.8,1,1.2,1.4,1.4,1.4,1.2,1,0.8,0.6 |
| ai_agency | 200000 | 250000 | 40000 | 4 | 50000 | 1 | 600000 | 5 | 1.0 | 0.6 | 100000 | 1 | 8 | all 1 |
| cleaning | 150000 | 15000 | 2000 | 80 | 40000 | 2 | 320000 | 120 | 1.3 | 0.4 | 30000 | 0 | 10 | 1,1,1.2,1.2,1.1,1,0.95,0.95,1,1,1,1.1 |
| logistics_box_truck | 3500000 | 90000 | 30000 | 22 | 300000 | 1 | 520000 | 26 | 0.8 | 0.2 | 50000 | 1 | 12 | 1,0.95,1,1,1,1,1,1,1.05,1.1,1.3,1.3 |

Scale names: coffee cart → coffee shop → chain; online store → shop → own brand; food truck → fleet → restaurant; freelance → retainer agency → SaaS; solo → crews → franchise; truck → fleet → 3PL (keys `industry.<id>.scale.0..2` in the UI table, Task 11).

- [ ] **Step 1: Write the failing tests** — `Six_chapter2_industries_load` (exact ids above, all `Kind == Business`, `Chapter == 2`); `Businesses_have_positive_margin_and_capex`; `Break_even_fits_in_one_staffed_unit` (`EconomyFormulas.BreakEvenUnits(FixedMonthly + Salary×(StaffPerUnit−1)×1.10, Price, UnitCost) <= CapacityPerUnit`); `Validator_rejects_business_without_capex`; `Validator_rejects_seasonality_not_12_long`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode IndustryContentTests` — Expected: compile errors / FAIL (new members missing).
- [ ] **Step 3: Implement** the fields, JSON and validator rules (business: Capex > 0, CapacityPerUnit > 0, StaffPerUnit ≥ 1, Seasonality length 12 when present, ScaleNameKeys length 3).
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): chapter 2–3 industry and balance data`.

### Task 2: Staffing, salaries, seasonality and district in the simulator

**Files:**
- Modify: `Core/Economy/BusinessState.cs` (add `int Employees`), `Core/Economy/BusinessSimulator.cs`, `Core/Game/GameEngine.cs` (callers)
- Test: `Core.Tests/Economy/BusinessSimulatorTests.cs`

**Interfaces:**
- Consumes: Task 1 fields.
- Produces: `BusinessInputs(double reputationFactor, double seasonFactor, double eventFactor, int workShare, double districtFactor = 1.0, double payrollOverhead = 0.0)`. Business capacity = `floor(CapacityPerUnit × Units × staffing)`, `staffing = min(1, (Employees + 1) / (Units × StaffPerUnit))` (the owner is one staff member); Fixed = `FixedMonthly × Units + round(Salary × Employees × (1 + payrollOverhead))`; demand gets `× districtFactor`. The engine passes `seasonFactor = Seasonality[(Month − 1) % 12]` and `districtFactor = DistrictDemand[district of s.Chapter]`.

- [ ] **Step 1: Write the failing tests** — `Owner_alone_runs_one_coffee_cart_at_full_capacity` (Units 1, Employees 0 → capacity 3000); `Food_truck_needs_a_hire_for_full_capacity` (Employees 0 → 1250 units cap; Employees 1 → 2500); `Salaries_with_payroll_overhead_are_fixed_costs` (2 × 300000 × 1.10 = 660000 plus 350000); `Units_multiply_capacity_and_fixed_costs`; `Seasonality_and_district_multiply_demand` (Jul food truck 1.4 × queens 1.15); `Closed_business_sells_nothing_but_still_pays_staff` (Units 0, Employees 2).
- [ ] **Step 2: Run** `tools/unity.sh test-editmode BusinessSimulatorTests` — Expected: FAIL.
- [ ] **Step 3: Implement** in `BusinessSimulator.Simulate` and pass the new inputs from `GameEngine.EndMonth`.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass (stage-1 gig tests unchanged).
- [ ] **Step 5: Commit** `feat(core): staffing, salaries, seasonality and district demand`.

### Task 3: Units and staff levers

**Files:**
- Modify: `Core/Game/MonthDecisions.cs` (document levers `"units"`, `"staff"`), `Core/Game/GameEngine.cs` (`ApplyLevers`), `Core/Game/MonthReport.cs` (add `Money Capex`, `Money AssetSales`, `List<string> RejectedLevers`)
- Test: `Core.Tests/Game/ExpansionTests.cs`

**Interfaces:**
- Produces: lever `"units"` = target owned units: opening pays `Capex × delta` now (rejected with `"units:<id>"` in `RejectedLevers` when cash < cost); closing refunds `Capex × AssetResaleRate` per unit. Lever `"staff"` = target employees, clamped to `0..Units × StaffPerUnit × 2`. Facts set by the engine: `first_hire`, `second_unit`, `price_changed` (first price ≠ industry price), `marketing_started` (first marketing > 0), `first_payroll` (first month with salaries). Gigs ignore `"units"`/`"staff"`.

- [ ] **Step 1: Write the failing tests** — `Opening_a_unit_pays_capex_from_cash`; `Opening_a_unit_without_cash_is_rejected`; `Closing_a_unit_refunds_half_capex`; `Staff_target_hires_and_fires`; `First_hire_and_second_unit_set_facts`; `Gig_levers_ignore_units_and_staff`; `Price_change_and_marketing_set_facts_once`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode ExpansionTests` — Expected: FAIL.
- [ ] **Step 3: Implement** in `ApplyLevers` (capex and refunds hit cash and the report before the month simulates).
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): open and close units, hire and fire`.

### Task 4: Bank loans

**Files:**
- Create: `Core/Economy/Loan.cs`
- Modify: `Core/Game/GameState.cs` (`List<LoanState> Loans`), `Core/Game/GameEngine.cs`, `Core/Game/DecisionLog.cs` (`TakeLoan = "takeLoan"`), `Core/Game/MonthReport.cs` (`Money LoanPayments`, `Money Interest`)
- Test: `Core.Tests/Economy/LoanTests.cs`

**Interfaces:**
- Produces: `public sealed class LoanState { string Id; Money Principal; Money Balance; double Apr; int MonthsLeft; Money Payment; }`; `GameEngine.MaxLoan(GameState s) : Money` = `MaxBase + Reputation × MaxPerReputation − Σ Balance`; `GameEngine.LoanApr(GameState s) : double` = `BaseApr + max(0, 50 − Reputation) × AprPerMissingReputation + (Honesty < LowHonestyBelow ? LowHonestyAprAdd : 0)`; `GameEngine.TakeLoan(GameState s, Money principal, int months) : bool` (false when above `MaxLoan`, term not in `TermsMonths`, or principal ≤ 0; true → cash += principal, fact `first_loan`, logged). In `EndMonth`: interest = `round(Balance × Apr / 12)`, payment = `min(Payment, Balance + interest)`, balance −= payment − interest, cash −= payment; loan removed at zero. Interest is deducted before the tax reserve: `TaxReserve(OperatingProfit − Interest)`, `NetProfit = OperatingProfit − Interest − TaxReserve`.

- [ ] **Step 1: Write the failing tests** — `Payment_matches_the_annuity` ($10,000, 12%, 24 months → 47073 cents); `Balance_reaches_zero_at_the_end_of_the_term`; `Loan_above_the_limit_is_rejected`; `Unknown_term_is_rejected`; `Low_reputation_and_honesty_raise_apr`; `Payments_continue_when_cash_is_negative` (in_debt set, loan still there; a $300,000 balance stays exact); `Interest_is_deducted_before_the_tax_reserve`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode LoanTests` — Expected: FAIL.
- [ ] **Step 3: Implement** `Loan.cs` and the engine changes (use `EconomyFormulas.LoanPayment`).
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): bank loans with annuity payments`.

### Task 5: Demand modifiers from events

**Files:**
- Modify: `Core/Narrative/Effects.cs` (new `DemandModifierEffect`, JSON key `"demand"`), `Core/Game/GameState.cs` (`List<DemandModifier> Modifiers`), `Core/Game/GameEngine.cs`
- Test: `Core.Tests/Narrative/NarrativeTests.cs`

**Interfaces:**
- Produces: JSON `{"demand": {"factor": 0.8, "months": 3, "industry": "current"}}` (`"current"` = the player's chapter-2 business; otherwise an industry id, validated at load). `DemandModifier { double Factor; int MonthsLeft; string IndustryId; }`. `EventFactor` for a business = product of its active modifiers; each modifier loses a month at the end of `EndMonth`.

- [ ] **Step 1: Write the failing tests** — `Demand_effect_scales_demand_for_its_months`; `Modifiers_expire`; `Two_modifiers_multiply`; `Unknown_industry_in_demand_effect_fails_at_load`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode NarrativeTests` — Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): event demand modifiers`.

### Task 6: Chapter flow — industry choice, goals, net worth, advancing

**Files:**
- Modify: `Core/Game/Chapters.cs`, `Core/Game/GameEngine.cs`, `Core/Game/DecisionLog.cs` (`ChooseIndustry = "chooseIndustry"`, `AdvanceChapter = "advanceChapter"`), `Core/Game/GameState.cs` (`CurrentVersion = 2`, `string District` derived: 1 bronx, 2 brooklyn, 3 queens), `Core/Persistence/SaveCodec.cs`
- Test: `Core.Tests/Game/ChapterFlowTests.cs`, `Core.Tests/Persistence/SaveCodecTests.cs`

**Interfaces:**
- Produces: `Chapters.NetWorth(GameState s, ContentDb c) : Money` = Cash + Reserve + Σ(Units × Capex × AssetResaleRate) − Σ loan balances; `Chapters.Chapter2GoalMet(s, c)` = fact `break_even` (engine sets it the first month a business with Units ≥ 1 has operating profit ≥ 0) ∧ NetWorth ≥ Chapter2CapitalGoal; `Chapters.Chapter3GoalMet(s, c)` = max Units ≥ Chapter3UnitsGoal ∧ (Σ Employees ≥ Chapter3EmployeesGoal ∨ NetWorth ≥ Chapter3CapitalGoal); `ChapterStatus(int Chapter, bool GoalMet, double Progress)` (0..1 for the HUD bar). `GameEngine.ChooseIndustry(GameState s, string industryId) : bool` (chapter ≥ 2, industry in `Chapter2Industries`, no business chosen yet → adds `BusinessState { Id = industryId, IndustryId = industryId, Units = 0, Price = def.Price }`, fact `industry_chosen`). `GameEngine.AdvanceChapter(GameState s) : bool` (current goal met → Chapter++, facts `chapter2_start` / `chapter3_start`; in chapter 3, `AdvanceChapter` returns false: chapter 4 is stage 4). Facts `chapter2_done`/`chapter3_done` are set like `chapter1_done`. `Replay` handles `chooseIndustry`, `advanceChapter`, `takeLoan`. SaveCodec migrates version 1 → 2 (Loans, Modifiers, Employees, Turns default). Housing follows the district (spec 3a): `Balance.PersonalCostsByChapter` — chapter 1 the Bronx room (stage-1 values), chapter 2 a Brooklyn studio (room 190000), chapter 3 a Queens apartment (room 230000), food/transit/essentials +10% per chapter; `EndMonth` uses the current chapter's costs.

- [ ] **Step 1: Write the failing tests** — `Chapter1_goal_lets_the_player_move_to_brooklyn`; `Cannot_advance_before_the_goal`; `Choosing_an_industry_adds_a_business_with_no_units`; `Second_industry_choice_is_rejected`; `Every_industry_can_be_started_from_the_chapter1_goal` (for each industry: cash = Chapter1CashGoal → open a unit directly, or `TakeLoan(capex)` succeeds at reputation 50); `Chapter2_goal_needs_break_even_and_capital`; `Chapter3_goal_needs_a_second_unit_and_staff_or_capital`; `Net_worth_counts_assets_at_resale_and_subtracts_loans`; `Rent_follows_the_district`; `Replay_reproduces_every_action_kind` (tutorial gig, scenes, quiz, chooseIndustry, units/staff levers, takeLoan, advanceChapter → same `StateHash`); `Version1_save_loads_as_version2`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode ChapterFlowTests` — Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): chapters 2–3 — industry choice, goals, net worth`.

### Task 7: Unit economics view

**Files:**
- Create: `Core/Economy/UnitEconomics.cs`
- Test: `Core.Tests/Economy/UnitEconomicsTests.cs`

**Interfaces:**
- Produces: `UnitEconomics.Compute(BusinessState b, IndustryDef d, BusinessMonthResult last, double payrollOverhead) : UnitEconomicsView { double GrossMargin; int BreakEvenUnits; Money Cac; Money Ltv; double LtvToCac; }` — break-even uses Fixed incl. salaries; new customers ≈ `unitsSold / RepeatPurchases`; `Cac = Marketing / max(1, newCustomers)`; `Ltv = (Price − UnitCost) × RepeatPurchases`; `LtvToCac = Cac == 0 ? double.PositiveInfinity : Ltv / Cac`.

- [ ] **Step 1: Write the failing tests** — `Coffee_cart_margin_and_break_even` (70%, 429 cups); `Ltv_is_margin_times_repeat_purchases` (5600 cents); `Cac_is_marketing_per_new_customer`; `No_marketing_means_infinite_ltv_to_cac`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode UnitEconomicsTests` — Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(core): unit economics (margin, break-even, CAC, LTV)`.

### Task 8: Pacing — daily turns, autopilot, daily missions, streak

**Files:**
- Create: `Core/Game/TurnBudget.cs`, `Core/Game/DailyMissions.cs`, `Content/missions.json`
- Modify: `Core/Game/GameState.cs` (`TurnState Turns`, `MetaProgress Meta`), `Core/Content/ContentDb.cs` (missions), `Game/Bootstrap/GameBootstrap.cs` (autopilot on launch, turn checks), `Game/Ui/UiRoot.cs` (HUD counter, disabled «Следующий месяц» with countdown)
- Test: `Core.Tests/Game/TurnBudgetTests.cs`, `Core.Tests/Game/DailyMissionsTests.cs`, `Game.Tests/TurnsUiTests.cs`

**Interfaces:**
- Produces: `TurnState { int Day; int ManualLeft; long LastSeenUnix; int Streak; int LastPlayedDay; }`; `TurnBudget.Refresh(TurnState t, long nowUnix, int utcOffsetMinutes, Balance b)` (local day = `floor((now + offset×60) / 86400)`; a later day refills `ManualLeft = DailyManualMonths`; an earlier day refills nothing); `TurnBudget.TryConsume(TurnState t) : bool`; `TurnBudget.SecondsToRefill(TurnState t, long nowUnix, int utcOffsetMinutes) : long`. Autopilot (`Autopilot.MonthsToRun/Run`, stage 1) runs on launch from `LastSeenUnix` and does not consume manual turns. `DailyMissions.For(int day, ulong seed, ContentDb c) : IReadOnlyList<MissionDef>` (3 a day, deterministic); missions reward founder-skill XP and `MetaProgress.CeoPoints` (future cosmetics), never cash; completing all three keeps the streak. `TurnState` and `MetaProgress` are not part of the replayed state (excluded from `StateHash`), so they cannot change rating results.

- [ ] **Step 1: Write the failing tests** — `Six_manual_months_per_local_day`; `Refill_happens_at_local_midnight_with_offset` (UTC−5); `Clock_set_back_grants_nothing`; `Time_zone_change_does_not_double_refill`; `Streak_counts_consecutive_days_and_resets_after_a_gap`; `Autopilot_months_do_not_use_manual_turns`; `Same_day_same_missions`; `Missions_reward_xp_and_points_never_cash`; PlayMode `Next_month_is_disabled_with_a_countdown_when_no_turns_left`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode TurnBudgetTests` and `DailyMissionsTests` — Expected: FAIL.
- [ ] **Step 3: Implement** core, then the HUD and bootstrap wiring (missions panel on the City screen).
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` and `tools/unity.sh test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat: daily turns, offline autopilot, daily missions and streak`.

### Task 9: Lessons for chapters 2–3

**Files:**
- Modify: `Content/lessons.json`, `Localization/Source/Academy.tsv` (EN + RU), re-import with `Editor/StringTableImporter`
- Test: `Core.Tests/Game/LessonTriggerTests.cs`

**Interfaces:**
- Consumes: facts from Tasks 3, 4, 6 and `receivable_gap` (engine sets it the first month cash < 0 while any receivable is outstanding).
- Produces 9 lessons, each with 3 quiz questions and a skill reward: `unit_economics` (industry_chosen, finance), `pricing` (price_changed, sales), `break_even` (break_even, finance), `llc` (chapter2_start, finance), `hiring` (first_hire, leadership), `payroll` (first_payroll, operations), `cash_gap_b2b` (receivable_gap, finance), `cac_ltv` (marketing_started, marketing), `loans` (first_loan, finance).

- [ ] **Step 1: Write the failing tests** — `Each_stage2_lesson_is_queued_by_its_fact` (table-driven over the 9 ids); `Receivable_gap_fact_is_set_when_cash_is_negative_with_receivables`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode LessonTriggerTests` — Expected: FAIL.
- [ ] **Step 3: Write** lessons (30–60 s of reading each, real NYC/SBA examples) and translations; set the fact in the engine.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass, including stage-1 `Lesson_keys_exist_in_en_and_ru`.
- [ ] **Step 5: Commit** `content: chapter 2–3 lessons`.

### Task 10: Scenes for chapters 2–3, rival and new NPCs

**Files:**
- Create: `Content/storylets_ch2.json`, `Content/storylets_ch3.json`, `Yarn/ch2_*.yarn`, `Yarn/ch3_*.yarn`
- Modify: `Content/npcs.json` (dialogue-only NPCs `banker_lee`, `supplier_rosa`, `journalist_mia`, `cofounder_sam`), `Yarn/Chapter1-ru.csv` → rename to `Yarn/Story-ru.csv` (update the yarnproject), `Core/Content/ContentDb.cs` (load the new files)
- Test: `Core.Tests/Content/StoryContentTests.cs`, `Editor.Tests/YarnProjectTests.cs`

**Interfaces:**
- Produces ≥ 48 scenes: 6 per industry (36, condition `industry`), 6 generic Brooklyn, 6 generic Queens; rival scenes `boss_fatcat_price_war` (chapter 2, `chapter2_done`, once; ignoring it applies `demand 0.8 × 3 months`) and `boss_fatcat_poach` (chapter 3, `chapter3_done`, once; refusing a raise removes 2 employees); arcs started: banker, supplier, journalist, co-founder. Every choice has visible effects (cash, demand, life stats, facts, NPC relation) and at least one scene per industry leads to a lesson.

- [ ] **Step 1: Write the failing tests** — `Every_industry_has_at_least_six_scenes`; `Chapter_end_rival_scene_fires_once_when_the_goal_is_met`; `Every_new_line_has_a_ru_translation`; `Scene_choice_lines_resolve` (existing `choiceLines` check covers the new files).
- [ ] **Step 2: Run** `tools/unity.sh test-editmode StoryContentTests` — Expected: FAIL.
- [ ] **Step 3: Write** scenes (Yarn + JSON + RU rows; lock column per stage-1 rule).
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `content: chapter 2–3 scenes, rival and new NPCs`.

### Task 11: Screens — industry choice, chapter move, team and expansion, unit economics, loans

**Files:**
- Create: `Game/Ui/IndustryScreen.cs`, `Resources/UI/Industry.uxml`, `Game/Ui/ChapterTransition.cs`, `Game/Ui/LoanDialog.cs`
- Modify: `Game/Ui/DecisionsScreen.cs` (units and staff steppers per business), `Game/Ui/CompanyScreen.cs` (unit economics, loans, capex and interest rows in the P&L and waterfall), `Game/Ui/UiRoot.cs` (HUD chapter progress bar from `ChapterStatus.Progress`), `Localization/Source/UI.tsv`
- Test: `Game.Tests/Stage2UiTests.cs`

**Interfaces:**
- Consumes: Tasks 3, 4, 6, 7.
- Produces: `IndustryScreen.Show(GameEngine e, GameState s)` (six cards: name, scale path, capex, margin, break-even units, main lesson, disclaimer; choosing calls `ChooseIndustry`, then offers "open the first unit" or the loan dialog when cash is short); `ChapterTransition.Play(int chapter)` (title card while the district streams in); `LoanDialog` (amount slider ≤ `MaxLoan`, terms from `TermsMonths`, APR and monthly payment shown).

- [ ] **Step 1: Write the failing tests** — `Industry_screen_shows_six_cards_with_capex_and_break_even`; `Disclaimer_is_visible`; `Choosing_a_card_adds_the_business`; `Short_cash_opens_the_loan_dialog_with_terms`; `Units_stepper_shows_why_it_was_rejected`; `Company_shows_unit_economics_after_a_month`; `Hud_shows_chapter_progress`. Add the new screens to `UiRoot.Tabs` so stage-1 `No_hardcoded_strings` and `No_text_overflow_in_ru` cover them.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode Stage2UiTests` — Expected: FAIL.
- [ ] **Step 3: Implement** (UXML in `Resources/UI`, styles in `Tokens.uss`).
- [ ] **Step 4: Run** `tools/unity.sh test-playmode` and `test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `feat(ui): industry choice, chapter move, team, expansion, loans`.

### Task 12: Art for Brooklyn and Queens (owner-approved estimate first)

**Files:**
- Create: `tools/art/stage2/jobs.json`, `Art/Imported/Buildings/building_{warehouse,office,cleaning_co,brownstone_row,queens_house}.glb`, `Art/Imported/Props/prop_{food_truck,box_truck,coffee_cart}.glb`
- Test: `Editor.Tests/ImportedArtTests.cs` (minimum building count 16 → 21, props 14 → 17; `KeyBuildings` += warehouse, office)

**Interfaces:**
- Produces prefabs used by Task 13.

- [ ] **Step 1: Estimate and ask** — concepts GPT Image 2.6 high 2k with the pilot bodega style reference (8 × 2.75 = 22 cr); key buildings warehouse and office on Tripo P2 max (2 × 40 = 80 cr); the rest Tripo H3.1 standard (6 × 9 = 54 cr); total ≈ 156 cr. Re-check prices with `get_cost`, show the owner, wait for approval (balance after stage 1: 175.52 cr).
- [ ] **Step 2: Update the test minimums and run** `tools/unity.sh test-editmode ImportedArtTests` — Expected: FAIL (assets missing).
- [ ] **Step 3: Generate, QC the concepts (isolated, solid, no ground slab), make 3D, shrink textures** (`tools/art/max/shrink_glb_images.py`), import with `ArtImportTool.ImportAll`.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `art: Brooklyn and Queens buildings and vehicles`.

### Task 13: Brooklyn and Queens districts in the game

**Files:**
- Modify: `Editor/DistrictBuilder.cs` (`BuildBrooklyn()`, `BuildQueens()`, shared block layout), `Game/City/CityCatalog.cs` (entries with `int MinUnits`), `Game/Bootstrap/GameBootstrap.cs` (district by `s.Chapter`, swap on `AdvanceChapter`)
- Test: `Game.Tests/DistrictTests.cs`, `Game.Tests/GraphicsBudgetTests.cs`

**Interfaces:**
- Produces: Addressable groups `district_brooklyn`, `district_queens` (PackSeparately); `CityCatalog.BuildingFor(string industryId, int units)`: coffee_cart → prop_coffee_cart (1) / building_coffee_shop (2+); resale_store → building_pawn_shop; food_truck → prop_food_truck (1–2) / building_pizza (3+); ai_agency → building_office; cleaning → building_cleaning_co; logistics_box_truck → prop_box_truck (1) / building_warehouse (2+).

- [ ] **Step 1: Write the failing tests** — `Brooklyn_and_Queens_load` ; `Districts_stay_within_draw_call_budgets_per_mode`; `Opening_units_upgrades_the_building_on_the_lot` (coffee cart → coffee shop at 2 units); `Bootstrap_loads_the_district_for_the_saved_chapter`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode DistrictTests` — Expected: FAIL.
- [ ] **Step 3: Implement**, rebuild with `DistrictBuilder.BuildAll`, render previews `tools/art/{brooklyn,queens}-{day,night}.jpg` and look at them.
- [ ] **Step 4: Run** `tools/unity.sh test-playmode`, `tools/unity.sh build-web`, `tools/check-build-size.sh` — Expected: pass; `Build/*` ≤ 20 MiB.
- [ ] **Step 5: Commit** `feat(city): Brooklyn and Queens districts`.

### Task 14: Voice for chapters 2–3 (owner-approved estimate first)

**Files:**
- Modify: `tools/voice/build_manifest.py` inputs, `Yarn/Voice/{en,ru}/*.mp3`, `Audio/Lessons/{en,ru}/*.mp3`, `Audio/Music/music_{brooklyn,queens}.mp3`
- Test: `Core.Tests/Content/VoiceCoverageTests.cs` (already generic over Yarn lines and lessons)

- [ ] **Step 1: Estimate and ask** — count characters of new spoken lines and the 9 lesson bodies × 2 languages (eleven_v4, ≈ 1 credit per character, one take per generation as in stage 1) plus 2 music loops of 45 s (≈ 675 cr each); check the ElevenLabs balance; show the owner; wait for approval.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode VoiceCoverageTests` — Expected: FAIL (clips missing).
- [ ] **Step 3: Generate** with the stage-1 pipeline (batches of ~10 lines joined by `[long pause]`, `split_batch.py`), import, `VoiceAddressables.Apply`.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` — Expected: all pass.
- [ ] **Step 5: Commit** `audio: chapter 2–3 voice and district music`.

### Task 15: Balance simulation and the pacing gate

**Files:**
- Create: `Core.Tests/Game/BalanceSimulationTests.cs`, `Core.Tests/Game/Bots.cs`
- Modify (tuning only): `Content/industries.json`, `Content/balance.json` (including `chapter1CashGoal`)

**Interfaces:**
- Produces: `OptimalBot.Decide(GameEngine e, GameState s) : MonthDecisions` (scans price ±30% in 5% steps for the best expected operating profit, hires when capacity-bound and a hire pays back within 3 months, opens a unit when cash ≥ 1.5 × capex, takes a loan for the first unit when needed, answers quizzes correctly, picks scene choices with the best cash + reputation outcome) and `CasualBot.Decide` (default prices, no marketing, hires only after the hiring lesson, first scene choice). Both play through `GameEngine` only (so replay holds).

- [ ] **Step 1: Write the failing tests** (5 seeds × 6 industries) — `Optimal_play_takes_at_least_10_24_36_months_per_chapter`; `Casual_play_finishes_chapter2_within_60_months_with_every_industry`; `No_industry_dominates` (each industry's optimal chapter-3 months ≥ 0.6 × the median of the others); `Optimal_play_never_goes_bankrupt`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode BalanceSimulationTests` — Expected: FAIL on at least the chapter-1 month floor (the $1,000 goal is reached in a few months today).
- [ ] **Step 3: Tune** data only, one change at a time; record each change in the ledger as a ruling with the before/after numbers.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` and `test-playmode` — Expected: all pass.
- [ ] **Step 5: Commit** `balance: chapters 1–3 pacing and industry tuning`, then build web, check size, deploy the preview (`/tmp/ghpages` → gh-pages) and verify in the browser.
