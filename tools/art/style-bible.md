# Unpaid CEO — style bible (stage 1, chapter 1: the Bronx)

## Look
- **Day:** stylized 3D tilt-shift diorama. Chunky, slightly toy-like proportions, soft warm sun, short shadows, shallow depth of field (DOF only in Optimal and Max).
- **Night / late chapters:** neon-noir. Same meshes; the global `_UnpaidNight` (0→1) drives emission ×0.15→×1.5, the URP volume swaps the day LUT for the neon LUT.
- **Shader:** every in-game material uses `UnpaidCeo/Stylized` (banded main light, cool shade tint, warm rim, neon emission). Generated textures keep their colours; the shader supplies the style.

## Palette (sRGB)
| Role | Day | Neon |
|---|---|---|
| Brick / walls | #B5563C, #D98E5F | #3A1F3D |
| Asphalt / shadow | #3B3F4A | #14121F |
| Sky / shade tint | #8FB8DE | #1B1440 |
| Accents | #F2C14E (taxi yellow), #2E8B57 (bodega green) | #FF3EA5, #2DE2E6, #F9F871 |
| UI money green | #3DDC84 | #3DDC84 |

## Characters
- Anthropomorphic animals, ~1.6 m, big heads (≈1:4), readable silhouettes from the isometric camera.
- Player species (stage 1): raccoon, fox, cat; 2 outfits each (hustler hoodie, thrift blazer).
- NPCs: mentor raccoon in a thrifted blazer (brand face), Tony the bulldog (bodega apron), Mr. Fins the shark (pinstripe suit), Jay the otter (streetwear), Mom (player's species, cardigan), Mr. Whiskerton the fat cat (huge coat, top hat).
- Pipeline: concept (A-pose, front view, plain background) → Meshy 7 image-to-3D, quad topology, rig + idle/walk/celebrate/think. Budget 15–22k triangles (max-quality takes land ~20.2k after rigging).

## Buildings and props
- Bronx walk-ups (4–6 storeys, fire escapes, water tanks), bodega with awning, laundromat, pawn shop, elevated subway segment, stoop, basketball court, bus stop.
- Pipeline: concept (3/4 isometric view, plain background) → Tripo (model chosen by the pilot), PBR. Budget: buildings 2–8k triangles at LOD0, props 0.2–3k; Mesh LOD ×2 and a collider added on import by `GlbPrefabBuilder`.

## Prompt template
`stylized 3D game asset, {subject}, chunky toy-like proportions, tilt-shift diorama style, warm daylight, clean PBR materials, isometric 3/4 view, plain light-grey background, no text, no logos`

## Pilot (decides the model for the batch)
One building (bodega walk-up) through Tripo H3.1, Tripo P2 and Meshy 7; concepts from Nano Banana Pro and GPT Image 2.6. Compare in Unity under `UnpaidCeo/Stylized`: silhouette, texture fidelity, triangle count after LOD, import errors. Result and the chosen model are recorded here.

## Pilot result (2026-11-08, 84.5 credits)
Concept: Nano Banana Pro chosen over GPT Image 2.6 — chunkier, closer to the toy-diorama look, no baked-in sidewalk or props (better for image-to-3D). Same concept fed to all three 3D models; all imported cleanly and passed the budget (`tools/art/pilot/report.md`, previews in `tools/art/pilot/*.jpg`).

| Model | Credits | LOD0 tris | Result |
|---|---|---|---|
| **Tripo P2** | 34 | 4 351 | closest to the concept: awning, storefront, fire escape, tank, rich colour; smallest file — **key buildings** |
| Tripo H3.1 | 9 | 5 430 | good silhouette and fire escape, weaker awning/storefront, washed-out colour — **background buildings and props** |
| Meshy 7 | 38 | 5 547 | lost storefront detail, 10.6 MB file — **characters only** (it rigs and animates) |

Decision: key buildings → Tripo P2; background buildings and props → Tripo H3.1; characters → Meshy 7 (quad, A-pose, rig, animations). Concepts → Nano Banana Pro. The Tripo P2 bodega is kept as `Art/Imported/Buildings/building_bodega.glb`.

## Chapter 1 batch (2026-11-08, 911.5 credits; owner approved the full batch ≈ 906)
- 37 concepts (Nano Banana Pro) → 36 models: 11 rigged characters (Meshy 7, idle clip, ~18.5k triangles), 3 key buildings (Tripo P2) + bodega from the pilot, 8 background buildings and 14 props (Tripo H3.1). Job ids and result URLs: `batch-jobs.json`; concepts: `concepts/`; in-engine previews: `previews/`; budget report: `import-report.md`.
- QC re-dos: `building_pizza` concept (background clutter), `building_court` concept and model (a see-through chain-link fence broke reconstruction — use solid barriers), `prop_phone` dropped from 3D (it is a UI element).
- Lesson for prompts: keep subjects solid and isolated — no see-through fences or nets, no pedestals, no depth-of-field background.
- Textures are capped on import by `GlbPrefabBuilder` (buildings and characters 1024, props 512, compressed); only base colour and emission are kept — the house shader supplies the style.

## Restyle (2026-11-08, 390.75 credits; owner approved the full restyle ≈ 415)
- Owner picked the detailed GPT Image 2.6 pilot bodega as the look for the whole district. All buildings re-made in that style: 17 concepts (GPT Image 2.6, high, 2k, the pilot bodega as style reference) → 16 models (Tripo P2, `face_limit` 8000, no PBR, standard texture: bodega, walk-up, laundromat, pawn shop + new coffee shop, barbershop, bakery, car wash; Tripo H3.1, `face_limit` 8000: the 8 background buildings). Jobs, URLs and spend: `restyle/restyle-jobs.json`; concepts and contact sheets: `restyle/`.
- Concept rule (updated): **concepts → GPT Image 2.6 with the pilot bodega as the style reference**; isolate the subject (no sidewalk slab, no street props), keep solid forms.
- `restyle/concepts/01_keyart_bodega_night.png` is night key art for promo and the neon look; in game, night is still lighting on the same meshes.
- New business buildings (coffee shop, barbershop, bakery, car wash) are imported as prefabs but not referenced by the Bronx scene or the CityCatalog, so they do not ship in the chapter-1 download; they go into the catalog with the chapter-2 (Brooklyn) industries.
- Known issue: the concepts' warm side light is baked into the textures (strongest on Tripo P2 models). Fix if needed: re-run Tripo P2 with `texture_version` v3.5 + `delight` (34 credits per building).

## Max-quality pass (2026-11-08; owner: "max quality for key buildings and characters")
- 8 key buildings (bodega, walk-up, laundromat, pawn shop, coffee shop, barbershop, bakery, car wash): Tripo P2, texture_quality extreme, PBR, texture v3.5 + delight (baked light removed) — 40 cr each. Embedded textures shrunk to 2048 with `max/shrink_glb_images.py` (8192/4096 → 2048, the game's app cap) to keep LFS small.
- 11 characters: Meshy 7 ultra, PBR, 19.5k triangles, A-pose (44 cr) + 4 × 3d_rigging takes (idle 0, walk 30 Casual_Walk, cheer 59 Victory_Cheer, think 36 Confused_Scratch; 8 cr each). The idle take is the model; the other takes are stripped to node hierarchy + animation (`max/strip_anim.py`, ~100-250 KB) in `Art/Imported/Characters/Clips/`. Fox and cat hoodie concepts re-made from their blazer twins so each species looks the same in both outfits.
- Textures: apps/desktop 2048 (props 1024), WebGL override 1024 (props 512); normal maps kept (WebGL 512) and used by `UnpaidCeo/Stylized` (`_NORMALMAP`). Character budget 15–22k (rigged takes land ~20.2k).
- Jobs, URLs and the per-step spend: `max/max-jobs.json`.

## Brooklyn and Queens (2026-11-08, 210 credits; owner approved option b)
- 8 concepts (GPT Image 2.6, high, 2k, pilot bodega style reference; all passed QC first time) → key buildings warehouse and office on Tripo P2 max (as the max pass, 40 cr); cleaning company, brownstone row, Queens house, food truck, box truck and coffee cart on Tripo H3.1 detailed (texture + geometry detailed, PBR, `face_limit` 8000; 18 cr).
- Props must land at 0.2–3k triangles: `stage2/fetch_stage2.py` simplifies them with meshoptimizer (`npx @gltf-transform/cli simplify`, UVs and textures kept; ~7k → ~2.7k) before import, then shrinks textures (buildings 2048, props 1024). Next time, ask Tripo for `face_limit` 3000 on props and skip the simplify step.
- Jobs, URLs and spend: `stage2/jobs.json`; concepts: `stage2/concepts/`; in-engine previews: `previews/`.
