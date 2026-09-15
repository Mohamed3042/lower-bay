# Structural correction findings — 2026-09-09

These measurements explain owner-reported defects. They do not override footage
with an invented reconstruction or certify a missing-floor count.

## Ramp destinations: a real construction mismatch

`LB_SEED_stair_concrete` contains two large ramps and four small trench stair
runs. The large ramps occupy X -35.2..-32 and 32..35.2, Z 4..12, reaching
Y 3.397 near **Z=12**. Their low ends are near Z=4. Existing elevated end decks
are around Z=-4..4: the high ramp ends do not meet those decks. Nominal level
entries in `levels.json` never proved this connection.

The first sampled route diagnostic accidentally traced the large ramps in reverse
(rising towards Z=4). Those samples must NOT be used as acceptance measurements.
`LowerBaySurfaceRepair.TraceRoutes` now traces the actual rise toward Z=12 and
separately samples a possible upper return at X +/-36.8. That return is only a
diagnostic line, not an approved footprint or an implemented gallery. Floor-hit
colliders are excluded from overhead obstruction counts; the test is still NOT
a continuous CharacterController traversal test.

Required next: reconcile the upper room/passage volumes with the chronological
footage, then build enclosed landings and real destinations at dataset clearances.
Do not add two arbitrary slabs or declare the upper levels finished.

## Flashing: measured partial coplanarity

The exact-triangle audit missed differently triangulated overlapping surfaces.
The axis-aligned overlap audit found same-facing wall/dado intersections, duplicate
dado corner patches, and rail-flange/sleeper-top intersections at Y=-1.08.

`surface_ownership_revision.json` assigns one visible owner per targeted patch.
The post-export pass clips underlying triangles, interpolating UVs/normals. It
preserves all collision meshes and all non-target render groups. The original
Blender export and master are retained unchanged; the derived repaired export
is what deployment feeds Unity. Regeneration reapplies this pass deterministically.

Targeted competing areas removed: wall under dado 167.177818 m2; duplicate dado
3.572095 m2; sleeper under rail flange 8.88 m2. This is removal of competing
surface coverage, not deletion of those objects. Source report now finds zero
same-facing overlaps for these three exact rule pairs.

Other contacts remain, including train wheels/rail sides and shell/dado caps.
Do not describe all flashing everywhere as resolved based on this limited pass.

## Legacy material correction

34 material roles now bind actual copied donor legacy-family ShaderLab assets:
Bumped Specular, Self-Illumin/Diffuse and the migrated particle glass shader.
Procedural preview surfaces are baked to mipmapped albedo/gloss and normal maps;
all render material assignments are checked against accidental Standard/PBR use.

Donor BumpSpec and illumination sources are explicitly restored shader sources,
not recovered byte-identical Lower Bay binaries. `Normal-Glossy.shader` was rejected
because it remains a Lambert extraction stub despite its Specular name. Donor
projects were read only; copied shader SHA256 hashes are recorded.

## Still unfinished

- Full footage-derived upper-room/route reconstruction and controller traversal.
- Long dark train corridor, moving train behavior and client-compatible collision.
- Remaining surface contacts and motion-based visual signoff.
- Real client scene components/MapId and legacy engine export verification.

A clean material/bake report is not a production-ready map claim.
