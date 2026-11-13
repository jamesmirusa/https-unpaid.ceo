# Stage 5B · Step 4 — Needs, mood badges, "wants"

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans.

**Goal:** the hero's state reads in a second: four need bars in the HUD (energy, close ones, knowledge, reputation)
that animate and float their change; a tap opens a panel with what raises each and what happens when it falls;
up to four mood badges (pure Core function, explain only, no balance effect); three "wants" — the next real steps
of the chapter, ticked with a sound when done, no reward.

**Spec:** `docs/superpowers/specs/2026-11-13-stage5b-living-city-top-look-design.md` («Состояние героя»), step 4.

## Core (pure, `Core/Life/HeroView.cs`)
- `HeroView.Needs(s)` → energy, relationships, knowledge (StudyXp, bar capped at 100), reputation.
- `HeroView.Moods(s, balance, lastReport)` → ≤ 4 of: burnout, shark debt, in debt, paycheck (1 week), rested,
  lonely, cushion — bad ones first; nothing changes balance.
- `HeroView.Wants(s, content, planned)` → ≤ 3 of: talk to a host with a waiting scene (done once talked this week),
  pay yourself to the reserve, take the open lesson's quiz, earn (overtime), rest when tired, study toward the next
  gig; done flags follow this week's plan.

## UI
- HUD: 2×2 need bars (icon, name, value, bar), each floats its change ("−10"); a tap opens the needs panel.
- Mood chips under the HUD (icon + name, weeks left), a tap = one line from life (toast).
- "Wants this week n/3" card in the city stack (collapsed to one line; open shows the three with checkboxes); a new
  tick plays the success sound and a toast.

## Tasks
1. Core tests → HeroView. 2. UI tests → HUD bars, needs panel, moods, wants. 3. Strings EN/RU. 4. Demo, suites,
build, deploy, ledger, review.
