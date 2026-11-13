# Stage 4B — weeks: plan (executed inline, TDD per task)

Spec: docs/superpowers/specs/2026-11-11-stage4-living-city-design.md §4B.

Design decisions (binding for this pass):
- The turn is a week (`GameState.Week`, 1-based); `Month = (Week-1)/4+1` is kept as a stored field the engine keeps in
  sync (facts, cooldowns, seasonality, lessons stay month-based). Save v3 migrates `Week = (Month-1)*4+1`.
- `EndWeek` is a new, exact weekly settlement; the legacy `EndMonth`/`BeginMonth` stay untouched so every old
  decision log replays bit-for-bit (server and client share the core). New logs write `endWeek`/`beginWeek`.
- Weekly economics: BusinessSimulator takes a period share (0.25): capacity and demand scale, fixed costs, payroll and
  marketing are a quarter; receivables are queued per week (delay months × 4; migration splits old monthly entries).
- Money moves when it moves: rent on week 1; food + transit every week (quarter, last week takes the remainder);
  essentials week 2; loan payments and interest on week 4; tax reserve every week from the week's profit.
- Action points are a monthly pool refilled on week 1 and spent across weeks; each free action still once a month.
- Life deltas per week = a quarter of the month's (rounded); scenes roll each week from `eventsPerWeekWeights`
  (default [3,1] ≈ one scene a month).
- Month counters (deep debt, team months, demand modifiers) tick on week 4.
- Pacing: `dailyManualMonths` 6 → 7 turns (weeks) a day; autopilot one week per `autopilotHoursPerMonth` hours (set 1).
- UI: "Next week", HUD week/month, the week's result lists what was paid and what falls due next week; the
  checklist is per week and asks for study every second week.

Tasks: 1 core week loop + tests · 2 save v3 + replay · 3 pacing/autopilot/missions · 4 bootstrap/UI/strings ·
5 full suites, build, deploy, ledger.
