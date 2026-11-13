# Stage 5A — Week of one-tap buttons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the plan screen's checkboxes, sliders and ± hours with one-tap cards that commit at once and lock until next week, with a weekly action pool, so every week has decisions to make.

**Architecture:** Core gets a per-turn pool (weekly 2 points, a legacy month keeps its 3) and refills it every turn, so "done" means "done this week". Week modes and gig focus are presets over the existing `MonthDecisions` shares (the decision log format is unchanged). The UI builds every choice from one `ChoiceCard`; the week's taps are kept in `PlayerMeta` so a reload cannot undo them. Pacing is re-measured with a weekly bot run and tuned by energy costs, not by unrealistic pay.

**Tech Stack:** Unity 6000.6.2f1, C# 9, UI Toolkit, NUnit (EditMode/PlayMode), .NET Cloud Code server tests.

**Spec:** `docs/superpowers/specs/2026-11-12-stage5-week-dialogue-story-design.md` (section «Неделя из кнопок»; delivery step 1).

## Global Constraints

- One tap = decision taken; no undo; a closed card always says why and when it opens (spec «Неделя из кнопок»).
- Weekly pool: base 2, −1 when energy < burnout threshold (30), never below 1; each free action at most once a week.
- A legacy month (`EndMonth`, challenge/duel scenarios) keeps the monthly pool `BaseActionPoints` = 3 and stays bit-identical otherwise.
- Every player-facing string EN + RU in `Localization/Source/UI.tsv`, then `tools/unity-run.sh UnpaidCeo.Editor.StringTableImporter.ImportAll strings`; Inter must cover every glyph.
- Money in actions stays realistic and sourced (`balance.json` → `sources`); pacing is tuned with energy, rest and points.
- Optimal weekly play needs ≥ 40 weeks for chapter 1 and within ±15 % of the baseline median measured in Task 1.
- Web build ≤ 20 MB (`tools/check-build-size.sh`).
- TDD: each test seen RED, then GREEN; EditMode, PlayMode and server suites green before each commit.
- Never commit `link.xml(.meta)`, `addressables_content_state.bin`, `lib_burst_generated.wasm`, `*-generated.ysls.json`, `game/Assets/Resources*`.
- Commits end with the two attribution lines (`Co-Authored-By: Claude Opus 5.5 …`, `Claude-Session: …`); branch `feat/stage5-story`.
- Ledger: `.superpowers/sdd/2026-11-12-stage5a-week-of-buttons/progress.md` (added with `git add -f`), one line per task, `Ruling:` lines with cost if wrong.

## Review Focus

1. Reload in the middle of a week after tapping actions → the same cards are still done and energy still shows spent (no free undo).
2. A week with no points left or too little energy for any earning action → every card says why, and «Следующая неделя» still works.
3. An old v3 save loaded mid-month (with last month's `ActionsThisMonth`) → opens with this week's fresh pool, no crash.
4. A chapter-2 owner with a unit (no gigs pay) → the focus block is hidden; modes and levers still work.
5. Switching language while cards are locked → statuses re-render in the new language.

---

### Task 1: Weekly balance harness and baseline

**Files:**
- Modify: `game/Assets/UnpaidCeo/Core.Tests/Game/Bots.cs` (add `BotRunner.RunWeekly`)
- Create: `game/Assets/UnpaidCeo/Core.Tests/Game/WeeklyPacingTests.cs`

**Interfaces:**
- Produces: `static RunResult BotRunner.RunWeekly(ContentDb db, ulong seed, string industry, IBot bot, int maxWeeks = 1600, int stopAfterChapter = 1)` — same loop as `Run`, but `BeginWeek` + scene choice + `EndWeek`; `RunResult.ChapterWeeks[int]` (new dictionary, weeks spent in each chapter).

- [ ] **Step 1: Write the test** `Weekly_chapter1_pacing_is_recorded` — runs `OptimalBot` and `CasualBot` weekly on the 5 seeds (chapter 1 only), writes `TestResults/weekly_balance.txt` (seed, optimal ch1 weeks, casual ch1 weeks) and asserts every optimal run finished chapter 1.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode WeeklyPacingTests` — Expected: FAIL (`RunWeekly` not defined).
- [ ] **Step 3: Implement `RunWeekly`** (bots decide once per week; `Bots.cs` free-action picks already respect `ap`).
- [ ] **Step 4: Run again** — Expected: PASS; copy the table into the ledger as `Task 1: baseline (monthly pool, weekly turns): optimal ch1 median N weeks, min M; casual median K`.
- [ ] **Step 5: Commit** `test: weekly pacing harness and chapter-1 baseline`.

### Task 2: A fresh pool every turn

**Files:**
- Modify: `Core/Life/LifeBalance.cs`, `Core/Life/LifeSimulator.cs`, `Core/Game/GameState.cs`, `Core/Game/GameEngine.cs:48,306-322`, `Core/Game/ActionChoices.cs`, `Core/Persistence/SaveCodec.cs:77-79,104`, `Content/balance.json` (`life.baseWeeklyActionPoints: 2`)
- Test: `Core.Tests/Game/WeekTests.cs`, `Core.Tests/Game/ActionChoicesTests.cs`, `Core.Tests/Persistence/*` (save migration test file already present)

**Interfaces:**
- Consumes: `RunWeekly` (Task 1) only for the ledger re-measure.
- Produces: `LifeBalance.BaseWeeklyActionPoints` (int, default 2); `static int LifeSimulator.WeeklyActionPoints(LifeStats s, LifeBalance b)` = `Math.Max(1, BaseWeeklyActionPoints − (Energy < BurnoutThreshold ? 1 : 0))`; `GameState.ActionsThisTurn` (`List<string>`, `[JsonProperty("ActionsThisMonth")]` so saves keep loading); `ChoiceState.DoneThisTurn` (renamed from `DoneThisMonth`); `ActionChoices.PointsTotal(s, c)` = `WeeklyActionPoints`.

- [ ] **Step 1: Write failing tests** in `WeekTests`: `Each_week_refills_two_action_points` (week 1 takes overtime+rest → both accepted; week 2 same plan → both accepted again), `A_third_action_in_a_week_is_rejected`, `Burnt_out_hero_has_one_action_a_week` (energy 20 → second action in `report.RejectedActions`), `A_legacy_month_still_gets_three_points` (`EndMonth` with three actions → none rejected). Update the old monthly-pool assertions (`WeekTests.cs:110`, `ActionChoicesTests` pool-across-weeks and done-this-month cases) to the weekly rule.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode WeekTests` — Expected: FAIL on the new tests.
- [ ] **Step 3: Implement** — `Settle` refills at the start of every turn: `s.ActionPointsLeft = weekly ? WeeklyActionPoints : ActionPoints(monthly)` and clears `ActionsThisTurn`; `NewGame` uses the weekly value; `ActionChoices.State` no longer reads last turns (only this plan); `SaveCodec` keeps its null-fill under the new property name.
- [ ] **Step 4: Run** `tools/unity.sh test-editmode` and `cd server/cloudcode && ~/.dotnet/dotnet test` — Expected: all green.
- [ ] **Step 5: Test for Review Focus 3** `Old_save_mid_month_opens_with_a_fresh_week_pool` (decode a v3 JSON with `"ActionsThisMonth":["overtime"]`, `"Week":6` → `ActionChoices.State(..., "overtime") == Open`). RED → GREEN.
- [ ] **Step 6: Commit** `feat(core): action pool refills every turn — 2 a week, once a week each`; ledger `Ruling:` the known replay limit (ruling #6: a content patch unranks old careers until server checkpoints) applies — cost if wrong: careers started before this build stop ranking.

### Task 3: Week modes and gig focus

**Files:**
- Create: `Core/Game/WeekPlanning.cs`, `Core.Tests/Game/WeekPlanningTests.cs`
- Modify: `Core/Content/Balance.cs` (`Dictionary<string, int[]> WeekModes`), `Core/Content/ContentValidator.cs`, `Content/balance.json`

**Interfaces:**
- Produces (all `static` in `WeekPlanning`): `string[] ModeOrder = { "hustle", "balance", "study", "family" }`; `void ApplyMode(MonthDecisions d, string mode, Balance b)`; `string ModeOf(MonthDecisions d, Balance b)` (null when the shares match no mode); `const string EvenFocus = "even"`; `void ApplyFocus(MonthDecisions d, IReadOnlyList<string> gigIds, string focus)` (gig id → 100 there and 0 elsewhere; `even` → `TimeAllocation.Rebalance` split summing 100); `string FocusOf(MonthDecisions d, IReadOnlyList<string> gigIds)`.
- `balance.json` → `"weekModes": { "hustle": [70,10,15,5], "balance": [55,15,20,10], "study": [45,10,20,25], "family": [45,25,25,5] }` (work, family, rest, study — starting values; Task 4 may retune).

- [ ] **Step 1: Write failing tests**: `Every_mode_sums_to_100_and_balance_is_the_default` (`ApplyMode(d,"balance")` equals `MonthDecisions.Default` shares), `Hustle_earns_more_and_tires_more_than_balance` (one `EndWeek` each from the same state: revenue higher, energy lower), `Focus_puts_all_work_into_one_gig`, `Even_focus_splits_work_evenly`, `ModeOf_recognises_a_mode_and_returns_null_for_custom_shares`, and a validator test `A_mode_that_does_not_sum_to_100_is_reported`.
- [ ] **Step 2: Run** `tools/unity.sh test-editmode WeekPlanningTests` — Expected: FAIL (types missing).
- [ ] **Step 3: Implement** `WeekPlanning`, `Balance.WeekModes`, validator rule (4 non-negative ints, sum 100, `balance` present).
- [ ] **Step 4: Run** EditMode + server suites — Expected: green.
- [ ] **Step 5: Commit** `feat(core): week modes and gig focus as presets over the plan`.

### Task 4: Weekly pacing tuned by energy (realistic pay)

**Files:**
- Modify: `Content/free_actions.json`, `Content/balance.json` (`life` and `sources`), `Core.Tests/Game/WeeklyPacingTests.cs`

**Interfaces:**
- Consumes: `RunWeekly` (Task 1), weekly pool (Task 2), modes (Task 3).

- [ ] **Step 1: Write failing test** `Optimal_weekly_play_takes_40_weeks_or_more_for_chapter1_and_stays_near_the_baseline` — every optimal run ≥ 40 weeks; median within ±15 % of the Task 1 baseline (constant copied from the ledger). Run → Expected: FAIL (the weekly pool makes chapter 1 faster).
- [ ] **Step 2: Look up and source** the NYC minimum wage for 2026 (official ny.gov / nyc.gov page) and the delivery minimum pay already sourced; set overtime/night-shift pay to hours × that wage, each with a `sources` entry (field, url, checkedOn, note). Pay stays realistic.
- [ ] **Step 3: Tune** energy costs of earning actions, `rest` recovery and, if needed, `life.restCoef`/`burnCoef` until the test passes; keep `rest` the one sure way back. Record final values and the new medians in the ledger with a `Ruling:` line.
- [ ] **Step 4: Run** the whole EditMode suite (including the monthly `BalanceSimulationTests`) and server tests — Expected: green.
- [ ] **Step 5: Commit** `balance: weekly pacing held by energy; overtime and night shift paid at NYC minimum wage (sourced)`.

### Task 5: One ChoiceCard, no undo in rooms

**Files:**
- Create: `Game/Ui/ChoiceCard.cs`
- Modify: `Game/Ui/VisitPanel.cs`, `Game/Ui/PlacesScreen.cs:167`, `Game/Ui/DecisionsScreen.cs` (`SetPlanned` → `Plan`), `Localization/Source/UI.tsv`, `Resources/UI/Tokens.uss`
- Test: `Game.Tests/ChoiceTests.cs`

**Interfaces:**
- Consumes: `ChoiceState`, `ActionChoices` (Task 2).
- Produces: `sealed class ChoiceCard : Button` — `ChoiceCard(string name, string titleKey, Action onTap)`; `void SetChips(Money cost, IEnumerable<Effect> effects, string lang)`; `void Show(ChoiceState state, bool recommended)` (enables only `Open`; status label `<name>-status` bound to the reason key, hidden for a plain open card); classes `choice-card`, `choice-card--done`, `choice-card--recommended`. `DecisionsScreen.Plan(string freeAction) : bool` (commits only when `Open`; there is no way to un-plan).
- Strings: `choice.done` = "✓ Done — again next week" / "✓ Сделано — снова на следующей неделе" (replaces `choice.planned`); `choice.no_points` = "No actions left this week" / "Действия этой недели кончились".

- [ ] **Step 1: Write failing tests**: `A_tapped_choice_is_done_and_cannot_be_undone` (tap bodega overtime twice → still planned once, card disabled, status = `choice.done` text), `Room_cards_say_why_they_are_closed_when_points_run_out` (Review Focus 2: plan two actions → third card disabled with `choice.no_points`; `next-month` button still enabled), `Statuses_follow_the_language` (Review Focus 5: switch to EN → status text is the EN string).
- [ ] **Step 2: Run** `tools/unity.sh test-playmode ChoiceTests` — Expected: FAIL.
- [ ] **Step 3: Implement** `ChoiceCard`; `VisitPanel` builds its plan choices with it and never un-plans; `PlacesScreen` calls `Plan`; strings imported.
- [ ] **Step 4: Run** PlayMode + EditMode — Expected: green.
- [ ] **Step 5: Commit** `feat(ui): one choice card; a tapped choice is done until next week`.

### Task 6: The Week screen

**Files:**
- Modify: `Resources/UI/Decisions.uxml`, `Game/Ui/DecisionsScreen.cs`, `Game/Ui/UiRoot.cs` (nav label, gate, meta save), `Game/Bootstrap/GameBootstrap.cs` (restore/save the week plan with the meta), `Core/Game/DailyMissions.cs` (`MetaProgress.WeekPlan`), `Core/Persistence/SaveCodec.cs` (`DecodeMeta` tolerates null), `Localization/Source/UI.tsv`, `Resources/UI/Tokens.uss`
- Create: `Core/Game/WeekPlanRecord.cs`
- Delete: `Game/Ui/DayPlanControl.cs`, `Game/Ui/ShareControl.cs` (+ `.meta`), `Resources/UI/Day.uss` if nothing else uses it; the day-plan tests in `Game.Tests/Stage4RhythmTests.cs` and `Game.Tests/UiSmokeTests.cs:88-94`
- Test: `Game.Tests/WeekScreenTests.cs` (new), `Game.Tests/Stage2UiTests.cs:165-232` (levers become one-tap)

**Interfaces:**
- Consumes: `ChoiceCard`, `DecisionsScreen.Plan` (Task 5); `WeekPlanning` (Task 3).
- Produces: `sealed class WeekPlanRecord { int Week; string Mode; bool ModeChosen; string Focus; bool FocusChosen; List<string> Actions; List<LeverChange> Levers; }`; `MetaProgress.WeekPlan` (null by default); on `DecisionsScreen`: `string Mode`, `bool ModeChosen`, `bool ChooseMode(string mode)`, `string Focus`, `bool FocusChosen`, `bool ChooseFocus(string focus)`, `bool LeverLocked(string businessId, string lever)`, `WeekPlanRecord Snapshot()`, `void Restore(WeekPlanRecord r)` (ignored unless `r.Week == state.Week`); `StepPrice/StepMarketing/StepUnits/StepStaff` apply one step and lock that lever for the week; `Build()` keeps its contract.
- Element names: `mode-hustle|balance|study|family`, `focus-<gigId>`, `focus-even`, `action-<id>` (ChoiceCard), `price-minus-<id>`, `price-plus-<id>`, `marketing-minus-<id>`, `marketing-plus-<id>`, `units-plus-<id>`, `staff-minus-<id>`, `staff-plus-<id>`, value labels `price-value-<id>` etc. showing "now → after" once changed.
- Rules: the mode of last week is pre-selected; tapping any mode (also the current one) confirms it, sets `TurnGate.PlanReviewed`, and locks the other modes for the week (status `week.mode_locked`). Focus works the same and is hidden when the owner has a unit. A gig whose `RequiredStudy` is not met shows `focus.needs_study` and is disabled.
- Strings (EN / RU): `dock.decisions` Week / Неделя; `decisions.hint` "One tap and it's decided. Choices open again next week." / «Одно нажатие — и решено. Выборы откроются снова на следующей неделе.»; `week.mode` Week mode / Режим недели; `mode.hustle|balance|study|family` Hustle / Пахать, Balance / Баланс, Study / Учёба, Family / Семья; `mode.<id>.desc` (one line of consequences each, e.g. «Больше дохода · силы ↓ · близкие ↓»); `week.mode_locked` "Set for this week" / «Выбрано на эту неделю»; `week.focus` Where I earn / Где зарабатываю; `focus.even` Split evenly / Поровну; `focus.needs_study` "Needs {0} knowledge" / «Нужно знаний: {0}»; `lever.price_down|price_up|marketing_down|marketing_up|hire|fire|open_unit` (Цена −5 %, Цена +5 %, Реклама −, Реклама +, Нанять, Уволить, Открыть точку); `lever.locked` "Changed this week" / «Изменено на этой неделе»; `gate.plan` "Choose the week mode" / «Выбери режим недели».

- [ ] **Step 1: Write failing tests** in `WeekScreenTests`: `Week_screen_is_cards_only` (no `Toggle`, `Slider`, `SliderInt` under the screen; four `mode-*` buttons), `Choosing_a_mode_locks_the_others_until_next_week` (+ `CurrentDecisions()` shares equal the mode; after `EndWeek` + `Refresh` the modes are open again), `Tapping_the_current_mode_confirms_the_week_gate`, `Focus_card_sends_all_work_to_that_gig`, `A_locked_gig_says_how_much_knowledge_it_needs`, `Owner_with_a_unit_sees_no_focus_but_modes_and_levers` (Review Focus 4), `Plan_survives_a_reload_in_the_same_week` (Review Focus 1: tap mode + overtime, `Snapshot` → new screen → `Restore` → overtime still done, energy after plan unchanged), `A_plan_from_another_week_is_ignored`; in `Stage2UiTests` rewrite the three lever tests to one-tap: `Price_plus_is_one_tap_and_locks_the_price_for_the_week`, `A_staff_change_from_a_scene_is_not_undone_next_month` (kept, one-tap hire), `Open_a_location_shows_why_it_was_rejected`.
- [ ] **Step 2: Run** `tools/unity.sh test-playmode WeekScreenTests` — Expected: FAIL.
- [ ] **Step 3: Implement** the UXML (sections: header with energy now → after and actions left; modes; focus; deeds as `ChoiceCard`s; business), `DecisionsScreen` rules above, meta save on `PlanChanged` and restore on boot, strings; delete the old controls and their tests.
- [ ] **Step 4: Run** PlayMode, EditMode and server suites — Expected: all green.
- [ ] **Step 5: Commit** `feat(ui): the Week screen — one-tap modes, focus, deeds and levers, kept across reloads`.

### Task 7: Ship and show

- [ ] **Step 1:** `tools/unity.sh build-web` then `tools/check-build-size.sh` — Expected: `BUILD EXIT 0`, Build ≤ 20 MB.
- [ ] **Step 2:** In Chrome on the local build (portrait window, full resolution screenshots saved): the Week screen fresh; after choosing a mode and two deeds (locked cards with reasons, HUD energy now → after); a room with done/closed cards; next week reopened. Check every text in RU and EN.
- [ ] **Step 3:** Commit any fixes (each with its test), push `feat/stage5-story`, `tools/deploy-web.sh`, confirm the live `.data` hash on `https://unpaid.ceo/index.html?t=$RANDOM`.
- [ ] **Step 4:** Ledger final line; report to the owner in Russian with the screenshots, the rulings, and that the server needs `ugs login` to pick up the new Core rules.
