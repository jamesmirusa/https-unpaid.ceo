# Stage 5B · Step 3 — Conversations, tones, trust levels, level bonuses

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Talking to a host is a scene: Core decides deterministically which scene waits at the host (≤ 1 story scene
per host per week, "!" over them while it waits); answers carry a tone icon in a fixed place, their price, and a
knowledge lock ("[Knowledge 30] Ask about the markup") shown in advance; the effect shows at once ("Tony +2 ♥"); trust
reads as hearts 0–10 (cold below zero); reaching a level is a short skippable moment with a real bonus from Core.
This stage: the mechanism, Tony's and Mom's scenes and live phrases on it, one bonus each.

**Spec:** `docs/superpowers/specs/2026-11-13-stage5b-living-city-top-look-design.md` («Люди: отношения, разговоры,
уровни»), main spec `2026-11-12-stage5-week-dialogue-story-design.md` («Разговоры с персонажами»).

## Core (server replays it)
- `StoryletDef`: `Host`, `Step`, `Reaction`, `Tones` (choice → friendly|business|joke|firm), `Requires` (choice → min
  knowledge). `ChooseStoryletOption` refuses a choice whose knowledge is missing.
- `HostScenes.Waiting(state, content, host)` (pure): eligible host scenes — reactions first, then by step. `GameEngine.TalkTo(s,
  host)` → the waiting scene becomes pending, the host is marked talked-to this week (`GameState.HostTalkWeek`), logged
  (`talkTo`, replayed; refused in scenario runs). Weekly random scenes exclude host scenes (legacy months unchanged).
- `Relationships.Level(trust)` (0–10, trust ÷ 10 for positive), `Relationships.Tier(trust)` (cold < 0 ≤ neutral < 20
  ≤ warm < 50 ≤ close).
- Level bonuses (`balance.json → levelBonuses`): Tony 3 → fact `tony_weekend_shift` unlocks deed `weekend_shift` (8 h at
  the NYC minimum wage, $136); Mom 3 → fact `mom_sunday_dinner` unlocks `sunday_dinner` (energy and close ones). Granted
  in Core after a scene choice and after a week (`GameEngine.ApplyLevelBonuses`), journaled, idempotent.
- `FreeActionDef.RequiresFact` → `ChoiceState.Locked` (hidden in the UI; refused by the engine).
- Content: Tony's 7 scenes and Mom's 4 get `host`/`step`/`tones`; new Tony scene `ch1_tony_markup` with a knowledge-30
  answer (EN + RU lines, unvoiced).

## UI
- Talk in a room (and "Call Mom" on the hero) → `TalkTo`; a scene plays in the room (dimmed edges, nameplate with name,
  place and hearts, typed line skippable by a tap); otherwise a live phrase by tier (3 per tier per host).
- Options: 2×2 grid, tone icon in a fixed corner (friendly top-left, business top-right, joke bottom-left, firm
  bottom-right), price chips, locked answers with their requirement.
- After an answer: "Tony +2 ♥" floats, heart pulses, sound; a level-up moment (1.5 s, tap skips): flash, sound, banner
  "Tony — level 3", one-line bonus.
- "!" badge over a host (and their place sign) while a scene waits.

## Tasks
1. Core: tones/requires/host fields + validator; HostScenes + TalkTo + log/replay; random pool exclusion; Relationships;
   level bonuses + RequiresFact. EditMode tests first; WeeklyPacing re-measure (bots talk to hosts); server tests.
2. Content: host fields, tones, new scene + Yarn EN/RU, new deeds, live phrases, strings.
3. UI: presenter (nameplate, typewriter, tone grid, locks, prices, trust float), level-up moment, Talk wiring, "!" badges,
   hero "Call Mom". PlayMode tests.
4. Demo renders, suites, build, deploy, ledger, roadmap, review.
