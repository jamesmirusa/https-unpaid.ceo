# UI icons in the game (stage 5B)

- **Game icons:** the owner's set `tools/art/stage5b/icons/` (README there), resized to 256 px (`sips -Z 256`) into
  `game/Assets/UnpaidCeo/Resources/UI/GameIcons/<name>.png` (the `icon_` prefix dropped). One set for the whole game.
- **Service icons:** Phosphor (MIT, `Resources/UI/Icons/LICENSE-phosphor.txt`), bold/fill weights, rasterized white
  at 96 px by `rasterize_icons.swift` (macOS AppKit, no downloads), tinted in USS.
- `slice_sheet.swift` slices a transparent N×N sheet; `make_shadow.swift` draws the 9-slice panel shadow.

Note: before `docs/AUTOPILOT.md` was read, a trial pair of sheets was made in GPT Image 2.6 (4 jobs, 11 credits,
inside the approved ≈250); it was superseded by the owner's set and is not in the game.
