# Stage 5B · Step 5 — City look: light, post by quality, evening, street life; the 6-axis board

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans.

**Goal:** late sun on brick with cool blue shade by day, a warm evening with lit windows and lamps, neon-noir at
night; soft glow, grading and a light vignette on strong devices, none on weak ones; residents look around; then the
6-axis board (palette, one style, light depth, silhouette, UI, visible feedback) — every axis ≥ 3, target 4.

**Spec:** `docs/superpowers/specs/2026-11-13-stage5b-living-city-top-look-design.md` («Город и персонажи», step 5);
world look: `tools/art/style-bible.md`.

## Design
- Shader `UnpaidCeo/Stylized`: `_WindowGlow` (0–1, per material). On facades (vertical faces) dark glass-blue texels
  glow warm as night falls; a world-space window-cell hash leaves ~40 % of windows dark. Set to 1 on buildings by
  `CityView` at load (props, cars and characters keep 0).
- `DayNeonController`: three stops — day → evening (blend 0.5: warm low sun, amber ambient) → night; `TimeOfDay.Evening`;
  two runtime volumes: *evening grade* (weight peaks at 0.5: warm filter, slightly lower exposure) and *quality look*
  (split toning warm highlights / blue shadows, vignette 0.18, a touch of contrast, sidewalks no longer blown out) —
  weight 1 in Optimal and Max, 0 in Eco (which also has post off).
- Settings → look: Day · Evening · Neon (saved); Evening uses the day UI theme.
- `StreetLife`: standing residents turn their heads/bodies slowly left and right (look around), out of phase.
- `CityLookDemo` renders day/evening/night × Eco/Optimal/Max before and after; the board is scored on those renders
  and the UI demos, recorded in the ledger.

## Tasks
1. Tests (PlayMode): evening sun is warm, night sun cool; the quality look volume carries vignette + split toning and is
   off in Eco; building materials get window glow, props don't; Settings has Evening and the light follows; residents
   turn while standing.
2. Implement. 3. Renders + board. 4. Suites, build, deploy, ledger, review.
