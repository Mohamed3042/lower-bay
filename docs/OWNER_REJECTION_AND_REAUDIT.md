# Lower Bay: owner-reported structural defects and reopened reference audit

2026-09-09. This supersedes any implication that the saved visual preview has
verified layout completeness or accurate original-map behavior. No Unity scene,
Blender scene, donor project or running editor was changed during this audit.

## Owner reports — unresolved

- Approximately two more floors/areas are missing.
- `LB_SEED_stair_concrete` leads nowhere at some destinations.
- `LB_SEED_rail_rail_CELL*` and `LB_SEED_wall_tile_d` flash as the camera moves.
- Original train traffic moves quickly through a substantially longer, dark track
  route; the current short static enclosure does not reproduce it.
- Study all three original clips, about seven minutes total, for precise replication.

## Evidence revisited in this pass

These are time-ordered extracted-frame observations, NOT a claim to have watched
all three full videos continuously. Full temporal route tracing remains unfinished.

- **Clip 3, 0:29.5–0:35.0, sheets 04–05:** concourse, low wall opening, passage/ramp
  and upper walkway form a continuous transition. A stair ending against a sealed
  surface is not an acceptable substitute. Exact elevations cannot be read from
  perspective images alone.
- **Clip 3, 0:43.5–0:53.0, sheets 06–08:** retreat from the upper approach into a
  passage, barred division and a broader branching tiled circulation space. This
  requires a connected route graph, not merely nominal level heights in JSON.
- **Clip 3, 2:10.5–2:12.0, sheet 24:** train front approaches along a long tunnel,
  train side passes the player, then the track clears. High-confidence inference:
  moving train traffic, not a stationary decorative train. Camera motion exists;
  exact speed, loop period, direction/reversal and hazard behavior are NOT measured.
- **Clip 1 chronological storyboard:** corroborates ramps, branching tiled passages,
  broader advertisement areas, upper crossing and long track view. It does not by
  itself prove an exact count of two missing storeys.

The supplied reconstruction seed and its previously frozen spatial assumptions
must be revalidated against footage. Preserve map identity and game scale, but do
not preserve an incorrect seed connection merely because it was previously frozen.
Do not invent two arbitrary slabs or metrically precise tunnel lengths from stills.

## Read-only source geometry measurements

`tools/audit_owner_geometry_defects.py` produced
`validation/measurements/owner_geometry_defect_audit.json`:

- Stair mesh spans Unity Y -1.2 to 3.397002, X approximately +/-35.2, Z -3.5 to 12.
  Bounds are not proof of connected destinations or walkability.
- Rail mesh spans X -38 to 38, Y -1.2 to -0.94, Z -0.78 to 1.12.
- No exactly coincident triangle pairs were found involving the three target source
  groups at 1e-6 coordinate rounding. This does NOT rule out partial coplanar overlaps,
  different tessellation, live scene duplicates, depth precision or material aliasing.
- The tile shader filters albedo seams, but its narrow normal-edge bands are not
  similarly footprint-filtered. This is a shimmer hypothesis, not a proven cause.

## Required correction gates

1. Trace the full clips in time order, recording every ascent/descent, entry, exit,
   re-entry and train passage. Keep observed/inferred/unknown distinctions explicit.
2. Reconcile route adjacency and level relationships; identify missing floor/room
   volumes and each stair's real destination. Update construction specs before build.
3. Reproduce flashing on the saved/live scene, isolate geometry versus shader causes,
   then fix the actual cause. No blanket material offsets.
4. Build the connected structure and extended tunnel/train behavior from corrected
   specs. Dynamic train rendering, collision/hazards, looping and reset behavior need
   an explicit runtime design grounded in footage and the UberStrike contract.
5. Test traversal continuously, including stairs and transitions; recapture all areas
   and only then rebake. Previous 28 tests and clean bake logs do not cover these gates.

## Budget / handoff

At the last read-only check, 1,478,112 / 1,500,000 uncached-input-plus-output tokens
were measured (13:22:31 UTC); this document/save work is later. The current cap is
not implicitly increased by a request to correct defects. No structural rebuild,
train animation or flashing fix has been applied in this pass.
