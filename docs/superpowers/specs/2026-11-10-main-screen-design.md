# Main screen: one place to play and learn (2026-11-10)

Owner's brief after playing the live build: "the game does not move to the next month; a new player cannot tell
what the tabs are for; one main screen is needed, with every task, action and event on it as popups and widgets;
no hero in sight, the city is empty; make it as easy as possible, and make it teach."

## What a player sees

One screen. The city is always on it; nothing replaces it.

| Area | What it is |
|---|---|
| HUD | cash (green, red below zero), month, energy; Journal and Settings |
| Chapter bar | the goal in words with its number, progress, "Next chapter" when met |
| City | the hero with a "You" tag in front of home, the mentor and Tony with name tags, people on the sidewalks, cars |
| Missions chip | "Daily missions 1/3"; a tap opens the list |
| Guide card | one sentence — what to do now — and one button that does it |
| Next month | the one primary button; says "Month in progress…" and is off while a month or a scene runs |
| Dock | Month plan · Report · Lessons · Ranking |

Everything else is a popup over the city: a dim backdrop, an opaque panel, a "Close" button, a tap outside closes.
HUD, "Next month" and the dock stay visible under a popup, so a month can always be ended.

On a wide window the layout stays a phone-wide column in the middle and the city shows on both sides.

## The guide card

First match wins:

1. chapter goal met → "Next chapter"
2. chapter 2+ without a business → "Choose a business"
3. the last month opened a lesson → "Open the lesson" (cleared when Lessons is opened)
4. no months left today → "Open lessons"
5. cash below zero → "Fix the plan"
6. otherwise → "Plan the month"

Hidden during onboarding (the mentor's coach marks speak then) and in challenge or duel runs.

## A month

Press "Next month" → the city plays the month (6 s) → the result popup: revenue, net profit, living costs, cash,
change over the month, and one line chosen from the numbers:

| Numbers | Line |
|---|---|
| no revenue | money comes only from sales |
| loss | costs were higher than revenue; find the biggest one |
| profit, cash down | living costs, purchases and loan payments are paid out of profit |
| profit, cash up | keep part as a reserve, put the rest to work |

"Got it" → the month's scenes, one after another (a modal with a full-width "Continue"; a tap anywhere continues) →
back on the main screen. "Full report" opens the report popup.

One thing at a time: result, then scenes, then (once) the question about anonymous statistics.

## The bug behind "it does not move to the next month"

A month with two scenes soft-locked the game. `YarnBridge` started the next scene from inside the previous one's
completion; the Yarn VM was still inside `Continue()` and dropped it, and `DialogueRunner` then cleared the new
dialogue's completion source. The panel stayed up with a dead button and the game stayed "busy" for good.
`YarnBridge.PlayAsync` now yields once after `DialogueTask`. Test:
`BootSmokeTests.A_month_with_two_scenes_plays_both_and_frees_the_game`.

## The city

- Every district is built on one plan (avenue along X, side street along Z, sidewalks 6.5 m from the centre lines,
  the home row 13.5 m north). `StreetLife` relies on it for lanes and for the spot in front of home.
- Hero: 2.6 m, on the sidewalk in front of home, strolls 2.6 m each way; the camera opens on it, raised above the
  area the UI covers. Creating the hero is a close-up with the hero standing still.
- Residents: the mentor beside the home door in every district; Tony in front of the Bronx bodega.
- Walkers: 10 (6 in Eco), the six hero models with tints. Taxis: `StreetCar`, not static.

## Rules for later work

- New screens are popups; nothing may hide the city permanently or "Next month".
- A button that cannot act must look off (`SetBusy`, `NextLocked`, no turns) — never alive and silent.
- Every visible word comes from `UI.tsv`; a single-class text colour needs its `.app …` twin at the end of
  `Tokens.uss` (the base rule `.app Label` is stronger than one class).
- Full-screen layers use the transparent-border bleed so they cover a wide window, with content inside the column.
