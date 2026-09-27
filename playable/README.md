# Lower Bay playable reconstruction

This continues the recovered September 22 authored map as a self-contained Unity
walkthrough and reproducible Blender export. The original `blender/map_master.blend`
and `LowerBay_v1.unitypackage` remain historical checkpoints. This candidate closes
the local-to-repository delivery gap; it is not a recovered original map binary.

## Open the map

**Prebuilt scene:** import [`LowerBay_Playable.unitypackage`](../export/unitypackage/LowerBay_Playable.unitypackage)
into a **Unity 6000.0.56f1, Built-in Render Pipeline** project with the legacy Input
Manager enabled. Open the generated `StrikeMap_lower-bay-reconstruction-v2_*` scene under
`Assets/StrikeMapStudio/Maps/` and press Play. Choose **Walk through** in the menu.
The package includes all referenced textures, meshes, scripts and the route fixture.
Other engine versions and URP/HDRP have not been verified for this review scene.

**Editable project:** open this folder's `unity/` project in that Unity version.
Run **Tools > Lower Bay > Build playable review scene**. This builds a new map
directory on every import and preserves prior scenes. The checked-in JSON is the
authoritative input; caches and local Windows binaries are ignored by Git.

WASD moves, mouse looks, Space jumps and Shift runs. **R** respawns; **T** pauses or
resumes the train; **Home** resets both train and player; **Esc** opens the menu.
Pickup markers can be collected and respawn after 20 seconds. Leave the train roof
before it carries you into a catwalk; a trapped rider is reset to the review spawn.

The Windows executable is built locally by `Build-Unity.ps1`. It is a map review
walkthrough, with no networking, login or original-game combat.

## What changed

- Recovered the complete station plan: connected upper rooms and returns, five
  glass-side entrances, two flank entrances, ticket hall, fare gates, service rooms,
  restroom, advertisement alcove, two-car train, six spawn markers and eleven pickups.
- Restored open catwalk tips at X=±18 and their 0.8 m committed drop. The previous
  wedges allowed walking back from the roof and changed the source gameplay.
- Closed the return-gallery floor gaps and the roof sections above both ramps.
- Applied a new AI-generated weathered steel texture to the train. All five
  textures ship with byte hashes and prompt provenance in [`generated/`](generated/).
- Added an actual Unity CharacterController walkthrough, train pause/reset,
  pickup triggers and blocked-rider recovery. The packaged player runs the route
  verification against its own scene and physics.
- Batched opaque visuals while preserving editable source geometry and separate
  collision. The train remains a moving group; transparent panels retain separate
  renderers. Texture tiling is baked into batch UVs before material sharing.
- Added portable compilation, regression checks, Blender export and package checks.

## Rebuild and verify

Node 22+ is sufficient; the map compiler has no npm dependencies:

```sh
cd playable
npm run build
npm test
node tools/prove_runtime_walk.mjs output/lower-bay.strikemap.json local/routes-new.json
```

Build the Unity package and Windows player, optionally baking lights:

```powershell
.\Build-Unity.ps1 -Unity 'C:/path/to/6000.0.56f1/Editor/Unity.exe' -Bake
```

The launcher prints the process ID, log and receipt location. A started process is
not a completed build: wait for it to exit and inspect `local/build/build.json`.
The Windows output is `builds/LowerBay/LowerBay.exe`. To exercise that executable:

```powershell
.\builds\LowerBay\LowerBay.exe -batchmode -lowerBayVerify 'C:/new-empty-path/proof' -logFile 'C:/proof.log'
```

The proof folder must not already exist. `runtime.json` must say `PASS`; all route
directions, committed drops, train carry/reset, pickup triggers and eight GPU
viewpoints are evaluated inside the actual standalone player.

Blender 5.1.2 exports the map in a fresh background process:

```sh
python tools/run_blender_scene.py --blender /path/to/blender --map output/lower-bay.strikemap.json --output /new/output --views overview,platform,red-upper-view,alcove-view,ramp-view
python tools/validate_blender_output.py --map output/lower-bay.strikemap.json --output /new/output --receipt /new/blender-proof.json
python tools/verify_package.py --receipt /new/package-proof.json
```

The Blender validator uses Pillow. Blender's builder does not require additional
packages. Output and receipt paths are exclusive to preserve earlier evidence.
Generated `arena.blend` has packed textures; `arena.glb` includes the train animation.

## Acceptance boundaries

See [`evidence/`](evidence/) for measured results. RED receipts preserve reproduced
defects; only the final receipts certify the current files. Geometry connectivity,
map-only physics, package integrity and visual inspection are separate checks.

Dimensions, hidden upper-room turns, the restroom service doorway, tunnel extension
and 7 m/s reversing train schedule remain explicit reconstruction estimates. New
textures and posters are AI interpretations. Train shape and station furniture are
simplified; this does not certify exact fidelity to every frame of the original clips.

The native adapter still supports importing into a compatible modern UberStrike
checkout, but **original-client combat, bots, authenticated sniper appearance,
legacy Unity compatibility and multiplayer have not passed full-game acceptance**.
The walkthrough's generic pickup visuals and local reset behavior do not replace
those systems. No existing game checkout, server or remote catalog is modified.

The five original PNGs total about 13 MiB, so this continuation bounds Unity imports
at 24 MiB per map and 16 MiB aggregate texture bytes (8 MiB per image, 4096 pixels per
axis). Signature, path, duplicate-name and decoder checks remain enforced.
