# Playable Lower Bay review — 2026-09-27

**Delivered:** a reproducible, playable reconstruction with a packed Blender file,
animated GLB, baked Unity package and locally built Windows walkthrough. This is a
map review candidate, not certification of the lost original map or a complete
UberStrike multiplayer integration.

## Recovered work and changes

Upstream `19f455fe689823fd7bfafd2d0180ade8cb836d99` contained the v1 design handoff.
The more recent local StrikeMap phase-2 plan had 910 entities and prior verification,
but no upstream PR. Its map-only compiler, collision checks, asset provenance and
Unity importer are now self-contained under `playable/`; no local authoring app or
machine-specific helper is required to rebuild.

Four defects were reproduced before correction:

1. Catwalk wedges allowed a player to walk from the train roof into an upper room.
   The open tips at X=±18 now preserve the committed 0.8 m drop.
2. The return gallery slabs did not reach their inner partitions. A player at
   X=±34.65, Z=8 fell to the ground. The slabs now meet the partitions. Door headers
   and short return walls also close visible exterior gaps in both upper rooms.
3. The real Unity controller could become blocked when the train carried a roof
   rider into a catwalk. It now detects failed carry and resets the rider to a clear
   spawn; direct body contact also resets. This is an explicit review-build hazard
   rule, not a recovered original damage model.
4. The connecting ramps had open ceiling sections. Roof slabs and doorway headers
   now close the overhead shell without blocking the measured player routes.

The alcove floor now meets its rear wall. New AI-generated steel is bound to both
train bodies and roofs. Opaque visuals are grouped by material and spatial section
without changing authored colliders, while moving train meshes remain attached to
the train. UV tiling is retained through material sharing. Added lights are baked,
with probes for moving geometry and separate lighting in the service rooms.

## Measured results

All final proofs bind to map SHA-256
`ea785e3fe2a3017365ae63308da9219a1acf377fe95344d93452fece66cec518`.

| Check | Result | Evidence |
| --- | --- | --- |
| Regression suite | 13 passed, 0 failed | [`tests-final.txt`](../playable/evidence/tests-final.txt) |
| Sampled support graph | 100% reachable and return-reachable; 6,706 support nodes | [`validation.json`](../playable/output/validation.json) |
| Deterministic route controller | All 45 route directions; committed drop and trapped-rider contract | [`routes-final.json`](../playable/evidence/routes-final.json) |
| Actual Windows player | All 45 route directions; train carry, pause, reset, crush/body-contact reset, pickups, upper-floor support | [`unity-runtime.json`](../playable/evidence/unity-runtime.json) |
| Unity scene | 930 authored entities, 280 authored colliders, 150 enabled renderers, 5 textures, 1 baked lightmap atlas | [`unity-build.json`](../playable/evidence/unity-build.json) |
| Fresh-project package import | Zero missing scripts; source and references intact; baked atlas present | [`package-import.json`](../playable/evidence/package-import.json) |
| Package integrity | 412 assets; current source, every texture and all C# scripts match exact bytes | [`package.json`](../playable/evidence/package.json) |
| Blender 5.1.2 | 930 entities, zero measured coordinate errors; 5 embedded images and 1 linear train animation in GLB | [`blender-final.json`](../playable/evidence/blender-final.json) |

Unity verification used 6000.0.56f1 and an NVIDIA GeForce RTX 5070 Ti. Renderer count
is an observed scene count, not a frame-rate benchmark. Eight actual Unity GPU
viewpoints are in [`preview/playable/`](../preview/playable/). Five Blender renders
are alongside the exported `.blend` and `.glb`. They show distinct engine lighting;
none is an AI mockup or a substitute for gameplay measurements.

The package SHA-256 is
`a93eb6058873b94eaa16b8afd0a460f5b46dbbf48931514c74d050c1986435a0`.
It was imported into a separate empty project after export. The original v1 files
and the user's pre-existing game/authoring checkouts were not modified.

## Remaining acceptance

- The original clips do not establish every hidden turn, doorway, metric dimension
  or train schedule. These remain labelled reconstruction choices in the plan.
- Train and furniture silhouettes, signage and AI artwork are simplified. Original
  material binaries and exact frame-for-frame fidelity are not certified.
- The walkthrough uses illustrative pickup markers. Native-game combat, bots,
  authenticated weapons, original-client callbacks, legacy Unity export and server
  synchronization still require an actual compatible game acceptance session.
- The original v1 Blender scene is preserved, not silently replaced by this authored
  continuation. Its earlier findings remain available for comparison.

The supplied [playable instructions](../playable/README.md) identify the supported
engine, controls, deterministic rebuild, native proof command and package checks.
