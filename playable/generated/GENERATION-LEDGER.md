# AI image generation ledger

Generated during the September22,2026 autonomous Lower Bay build using the image-generation tool. Original generated files remain under the Codex generated-images directory. Project copies are listed below; exact SHA-256 hashes for applied textures are in `ASSET-RECEIPT.json`.

## Reconstruction study

`lower-bay-reconstruction-study-v1.png`,2057×765. Original: `exec-a904860c-5184-416f-9df2-97c5c34a6663.png`.

Conditioned on the repository's inspected `clip1_storyboard.png`. Prompt requested a wide three-quarter cutaway of the entire approximately90×29m station: one sunken central track and two-car train, tiled platforms and square piers, glass-sided wide concourse, opposite narrow flank and advertisement alcove, upper end spawn rooms, dead-end catwalks, connected ramps and return galleries, pale ivory/black dado/cold fluorescent materials, subdued red and blue signage. Roof removed for inspection. No HUD or players. Unseen upper returns explicitly identified as inferred.

Use: composition/material guide for the authored semantic plan. It is a new interpretation, not a recovered original map or measured plan.

## Ivory wall tile

`ivory-tile-albedo.png`,1254×1254. Original: `exec-a89c4df5-7279-463a-abc5-5198829c15ea.png`.

Prompt: Create one seamless physically based base-color/albedo tile texture for a faithful2013-era abandoned subway game environment, Lower Bay. Square full-bleed orthographic surface, no lighting or perspective, no objects, labels or text. Regular8×8 grid of old ivory/grey ceramic square wall tiles, thin dark-grey grout, subtle uneven stained cream surfaces, dirt in grout, sparse hairline cracks and worn glaze. Muted cold neutral palette, believable underground station, no shiny marble or colorful patterns. Repeatable edges. Applied to actual wall and square-pier meshes.

## Concrete floor

`concrete-albedo.png`,1254×1254. Original: `exec-d14cd02d-f6f6-4d49-a4cc-ab5cea2d966c.png`.

Prompt: One seamlessly tiling albedo for a dark charcoal concrete subway floor. Square flat top-down texture, zero perspective/directional light, no text, objects, rails, tracks or yellow lines. Cool grey matte poured concrete, fine grain, old dust, shoe scuffs and small patched wear; restrained mid-dark values for realtime fluorescent lighting. Compatible with dirty ivory tiles and a2013 shooter. Avoid giant cracks, shine or wetness; repeatable edges. Applied to actual exported geometry.

## Station sign

`lower-bay-sign.png`,2172×724. Original: `exec-5a869959-ec63-4bfd-a5cc-a487b41c87d7.png`.

Prompt: A single flat very-wide subway wayfinding sign texture, no scenery, authentic worn2013-game atmosphere. Muted burgundy background, slender dirty-ivory border. Clear ivory sans-serif text: left 'Gideons Tower' with left arrow, centered 'LowerBay' and generic train pictogram, right 'Fort Winter' with right arrow. Fine scratches, readable, no additional words, sign fills image. Applied as an albedo graphic to station sign meshes.

## Actual use and limitations

The three texture PNGs are embedded unchanged in the generated map, referenced by entity materials, extracted in OBJ/Unity kits and packed into Blender/GLB. `IMAGE-ANALYSIS.json` records dimensions and measured mean colors. No original game textures were recovered. Seamless edges were requested but not certified by a tiling seam metric. Generated sign artwork is deliberately a new interpretation.

## Winter advertisement

winter-poster.png,1672x941. Original exec-d55328f7-d6ca-4fe7-b539-ff8cf68776a0.png. Prompt requested one flat landscape wall-poster texture: original faded2013 fantasy-game travel art showing frozen industrial towers and bridges in blue-white fog, snowy cliffs and dark foreground silhouettes; restrained cyan, paper grain and edge scuffs; no text, logos, characters, room, physical frame or perspective. Used in phase2 advertisement frames, replacing colored stand-ins. This is new AI artwork inspired by the video0:25 visual category, not recovered original game art.
