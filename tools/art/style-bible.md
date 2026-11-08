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
- Pipeline: concept (A-pose, front view, plain background) → Meshy 7 image-to-3D, quad topology, rig + idle/walk/celebrate/think. Budget 15–20k triangles.

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
