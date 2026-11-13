# Stage 5B · Step 2 — Radial menu, overhead badges, money calendar

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Tap a person, a place or the hero in the city (and the host in a room) → one radial menu of ≤ 6 items with
icon, short label, a chip with cost/effect or the reason it is closed. Badges over heads/signs ("$" work, "?" lesson)
pulse. The HUD shows the month's four weeks with their money dates (rent, essentials, loan) — green if the cash covers
it, red if not; a tap explains the cash gap.

**Spec:** `docs/superpowers/specs/2026-11-13-stage5b-living-city-top-look-design.md` («Круглое меню», «Календарь недели»,
«Город и персонажи»), delivery step 2.

## Architecture
- **Core** `Core/Game/MoneyCalendar.cs` — pure: `Month(GameState, Balance)` → 4 `CalendarWeek` (week of month, past/now,
  items, total, headline key, covered by cash) from `PersonalCosts.ForWeek` and `LoanDue(s)`; `GameEngine` uses the same
  `LoanDue` for "due next week" (one rule, one source). No state change, no save change.
- **UI** `Game/Ui/RadialMenu.cs` (view: petals on a circle, chip, backdrop; ≤ 6 items, no nesting; closed items say
  why; irreversible items need a second tap) and `Game/Ui/RadialMenus.cs` (the table: which actions a target offers —
  built from `Places` and the shared `ChoiceState` through `DecisionsScreen`, so rules don't change, only the way to
  tap). Order: talk/enter first, then recommended, then the rest.
- Targets: resident name tags (Tony → bodega, mentor → home), place signs, the hero's "You" tag, the host's head in a
  room. Tags/signs are UI elements over the 3D world, so taps need no 3D picking.
- `Game/Ui/Emotes.cs` — badge per target: "$" (an open earning deed there and points left), "?" (academy: a new lesson
  or study open). "!" (scene) and the heart (level up) come with step 3's dialogue mechanism.
- `Game/Ui/CalendarStrip.cs` in the HUD: header (week · n of 4 · action tokens) + 4 cells; a tap shows a toast tip.

## Tasks
1. MoneyCalendar (EditMode tests: weeks, headline, totals, covered/red, loans in week 4, bills before/after
   `BillsStartAfter`, engine DueNext unchanged) → server tests.
2. Calendar strip in the HUD (PlayMode: 4 cells, now marked, amount shown, red when not covered, tap → tip toast,
   language switch re-renders).
3. Radial menu view + table (PlayMode: Tony's tag → ≤ 6 items, enter first, deeds with icons; an open deed is planned
   in one tap; a closed one shows its reason and changes nothing; the loan shark asks twice; hero tag → rest/study/
   family; a place sign → enter first; the room host → that room's deeds; backdrop closes it).
4. Badges over heads and signs (PlayMode: "$" over Tony while a deed there is open; gone with no points; "?" on the
   academy sign with a new lesson).
5. Demo renders, full suites, build, deploy, ledger, roadmap, review.
