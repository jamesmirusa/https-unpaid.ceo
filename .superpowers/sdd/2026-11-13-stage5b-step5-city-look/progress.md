# SDD ledger — plan: docs/superpowers/plans/2026-11-13-stage5b-step5-city-look.md
Executor: inline (executing-plans), per docs/AUTOPILOT.md.
Baseline: CityLookDemo renders (city_before_*): noon sidewalks blown out to white, evening a grey dim of the day, no lit windows at night (the generated facades have no emission maps).
Task 1: CityLookTests 4 RED (compile: Sun/LookVolume/TimeOfDay.Evening/StreetLife.LookYaw missing; then windows: 0 instead of 1) → GREEN; UiKitTests.Settings_offer_an_evening_look GREEN.
Task 2: shader `_WindowGlow` (facades only; pale colourless glass texels turn warm as night falls; a window-cell hash leaves ~40 % dark), set on buildings by CityView (scene roots) and LotView (business buildings); DayNeonController three stops (day → warm evening → cool night), an evening grade volume (peaks at 0.5) and a quality look volume (split toning warm light / blue shade, vignette 0.18, contrast +8, exposure −0.22) on in Optimal and Max, off in Eco; Settings → Day · Evening · Neon (evening saved in PlayerPrefs, day interface); standing residents look around (two-sine sway, ±28°, out of phase).
Ruling: the project renders in gamma space and the facade textures carry no window masks, so window glow is detected from the texels (pale, colourless, on vertical faces) — the effect is clear on shaded facades at night and subtle on sunlit ones; a crisp version needs emission masks for the buildings (a Tripo/GPT re-texture — owner approval for credits) — cost if wrong: some grey trim or an AC unit can glow faintly.
Ruling: the day sun's colour and level moved to new field names (daySunLate, daySunLevel) so the scenes' serialized old values no longer override the warmer late-sun look — cost if wrong: a scene tweak of the old fields is ignored.
Ruling: no 3D raycasts or new street props for "street life" this stage; walkers and cars already move; residents now look around — cost if wrong: the street is as busy as before.
Board (6 axes, CityLookDemo city_after_* + UI demos kit_*, step2_*–step4_* + character previews):
palette: 4/5 — every UI colour from Tokens/Day/Neon; game icons in the bible's palette; scroll bars and buttons restyled, no default grey left.
style: 4/5 — toy-diorama 3D, glossy toy icons and cream/navy cards read as one game; neon theme has its own values for every token.
lighting: 4/5 — late warm sun with blue shade (split toning) by day, an amber evening with lit windows and lamps; night is 3 (windows subtle on sunlit facades).
silhouette: 4/5 — big-headed animals with ears/tails read at the street camera and in rooms.
ui: 4/5 — icon + label on every button, lips and shadows, 8-pt rhythm, styled scroll bars, radial and dialogue grids aligned.
feedback: 4/5 — sink on touch, tap sound, rolling cash with a floating change, bouncy popups, toasts, level-up moment, trust float, ticked wants.
Fix pass on the board: the HUD made compact (calendar head row removed), camera buttons in one row, mood badges into the card stack — the city view stays open above the cards.
Suites: EditMode 399/399, PlayMode 246 + 15 explicit demos, server 17/17.
