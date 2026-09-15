# Lower Bay build state

Branch: `Lower-Bay`.

## Current status

### 1.75M total cap; legacy materials and measured surface repairs — 2026-09-09

Owner explicitly approved TOTAL 1,750,000 uncached-input-plus-output tokens,
including all earlier spend. The new 1.4M 80% checkpoint was acknowledged by that
continuation approval; no reset or provider-enforced limit is implied.

34 material roles now bind hash-verified donor legacy ShaderLab assets (restored
Bumped Specular / Self-Illuminated Diffuse, migrated first-party glass), not PBR.
The broken donor Specular extraction stub was excluded. Texture maps now have
mipmaps, trilinear filtering and normal-map import settings. This is legacy-family
source provenance, not proof of recovered byte-identical Lower Bay shader binaries.

Three targeted coplanar repair rules remove competing wall/dado and rail/sleeper
coverage. Original Blender export and master remain intact; deployment deterministically
derives `export/preview/lowerbay_repaired_preview.json`. Collision data is unchanged.
36 tests pass. Render export is 193,576 triangles, plus 1,680 Unity wall-fixture
triangles = 195,256. Other contacts and full motion signoff remain open.

The initial reversed ramp diagnostic was corrected in source; only a subsequent
saved-scene check with the corrected helper is valid for route interpretation.
See `reference/STRUCTURAL_CORRECTION_FINDINGS.md`. No missing upper room, train
loop, client runtime wiring or legacy-engine export has been completed in this pass.
Bake/capture acceptance: Unity exited 0, one atlas, zero reported UV overlaps and
zero shader errors. Fresh saved-scene checks exited 0 with 551 renderers, 195,256
triangles, 41 colliders, zero material/lighting failures and zero dark-probe warnings.
All deployed helpers match source hashes. Platform overview plus three nearby
rail and dado camera poses were captured; inspected first/last poses show the
targeted shared surfaces intact. This is limited visual sampling, not continuous
motion certification. Corrected route diagnostics still report blocked upper
approaches and missing floors: **structural acceptance remains failed/open**.

New saved visual-check package: `LowerBay_VisualCandidate_20260909T140708.unitypackage`.
Package export also exited 0. Previous package/checkpoints remain available.

### Owner rejects layout completeness — structural/reference gates REOPENED

Owner reports missing floors, stairs leading nowhere, flashing rail/dado surfaces
and an undersized static train route. The previous preview's rendering/bake checks
do not certify these concerns. See `reference/OWNER_REJECTION_AND_REAUDIT.md` for
timecoded evidence, limited source-geometry measurements and the required rebuild
gates. Moving train traffic is corroborated by clip 3's 2:10.5–2:12 sequence; its
exact speed/loop behavior and missing floor count remain to be traced. No scene edits
were made during this read-only re-audit. Do NOT label the current package a faithful
or production-complete reconstruction.

### Saved, verified visual/collision candidate — 2026-09-09

Portable package exported successfully (Unity exit 0):
`Builds/LowerBayAstra/LowerBay_VisualCandidate_20260909T125759.unitypackage` in the
assigned Unity project; archived under `export/unitypackage/`. Latest handoff usage
checkpoint: 1,436,299 / 1,500,000 uncached-input-plus-output tokens through
12:58:43 UTC; subsequent save/push accounting is not included in that snapshot.

This supersedes running-bake and budget-pause notes below. Owner authorized continued
work within the unchanged 1.5M cap; no other open Unity project was modified or closed.

- Third corrective bake succeeded and saved: one atlas, 235 probes, no reported UV-overlap or shader errors; Unity exited 0.
- Final saved-scene check succeeded: **551 renderers, 194,500 render triangles, 41 separate colliders / 12,656 triangles, zero failures, zero dark-probe warnings**. The original 192,820 render triangles remain; the two wall cages add 1,680 render-only triangles.
- 19 remaining black sample locations were repaired using nearby positive samples within the same zone; the newly illuminated ad room resolved the other dark samples. The formerly black room is visibly lit in `Captures/qa_ad_room.png`.
- Neutral brushed train/rail/steel materials replace the rusty door-albedo mismatch. Train windows are less glaring; station lettering, lowered banner and modeled fixture detail are saved. Source-generation settings are updated for regeneration.
- **28 automated tests pass.** One old test assumed exactly nine JSON files; it now explicitly requires the nine core specs plus the bounded room-lighting revision and checks that revision's placement/clearance.
- Platform, upper approach, concourse, train, corridor and additional ad-room captures were inspected. Original camera limitations remain documented rather than hidden. Ad/graffiti artwork and some prop fidelity are still incomplete.
- Portable Unity visual-package export is the handoff format. This is NOT an in-game assetbundle or production certification: real MapId/client spawn components, controller traversal, remaining art approval and legacy export still need work. See `VERIFY_AND_PREVIEW.md`.
- Blender master is preserved unchanged from its saved 100-group export. The two new Unity wall cages are reproducible from `spec/room_lighting_revision.json` and the Unity helper; they are not falsely claimed to exist in that older Blender save.

### Owner-authorized finalization pass (supersedes pause below)

Owner acknowledged the 80% checkpoint and asked to finalize within the unchanged
1.5M total cap. First corrected bake completed successfully: Unity exit 0, one
lightmap atlas, zero reported UV overlaps, 537 renderers (22 lightmapped / 515 local
probe-lit), 41 colliders, 235 probes, no missing materials, incorrect render layers
or unbaked architecture. Render triangle total remains 192,820. All 27 tests pass.

The metal A/B capture exposed and replaced a rusty door atlas on the aluminum train
and rail/steel families with derivative-filtered neutral brushed metal. Window
emission is reduced; light lettering is made legible. This material-only revision
is receiving its own bake; the prior successful scene/materials/bake were backed up.
Do not treat the running bake as complete until its success marker and saved captures
have been checked. Original POV4 is pillar-occluded and POV5 is black; supplementary
floor-checked QA cameras cover the ad room and opposite upper approach.

Original advertisement/graffiti artwork has not been recovered. A narrow read-only
filename search in the supplied clients found character Godfather assets, not the
Lower Bay advertisement images; unrelated donor assets were not transplanted.
These art gaps and runtime release blockers remain explicit in `VERIFY_AND_PREVIEW.md`.

### 2026-09-09 resumed lighting correction / new 80% checkpoint

Owner increased the TOTAL cap to 1,500,000; all prior spend retained. At
11:49:45 UTC local telemetry reached 1,205,357 uncached input + output, so
development pauses at the new 1,200,000 checkpoint pending owner acknowledgment.
The older 1M stop below is historical and superseded.

- Licensing works outside the shell sandbox; the saved scene opened and diagnostic captures exited successfully.
- Probe refresh, BlendProbes and tetrahedralization alone did not restore black details. Baked SH coefficients were nonzero at platform samples. A neutral-material/known-lit-anchor comparison restored rail and prop shading, implicating global material-group sample locations (including bounds centred inside the train).
- Rendering-only spatial batches now use 6m horizontal cells / 3m vertical cells, anchored to nearby authored interior probe positions; samples inside the train are excluded. Collision, triangle positions, footprint and routes are unchanged. This is a corrective candidate, not yet bake-approved.
- Latest lettering/banner export and targeted padding fixes are deployed with full owned-folder backups under `<local>/_astra_backups`. Unity Build was already running when the guard reached its checkpoint; verify its completion before resuming. Do not mistake the newly rebuilt UNBAKED scene for the previous saved bake.
- Next authorized step: corrective bake, inspect all room captures, measure UV warnings, archive and package only after verification. Runtime components, real MapId and controller-level traversal remain unresolved; production is NOT certified.
- Accounting tests: 16 token-guard tests pass, including epoch carry-forward and hard-cap enforcement.
- In-flight build finished successfully: `LOWERBAY_ASTRA_PREVIEW_SUCCESS triangles=192820`, Unity exit 0. New scene is saved, but has NOT received its corrective bake. Archived previous bake reports/captures describe the historical 64-renderer scene, not this candidate. Renderer batching changed, so `preview_report.json` source-group count is not the actual new renderer count.

### Historical fork stop

**Budget stop after fork reconciliation:** parent restart audit omitted the active
fork's independent usage. Combined local estimate is 1,113,444 through 11:34:56 UTC;
see `measurements/usage_fork_audit.json`. The 1M cap remains unchanged. No Unity
execution, bake or geometry changes began in this continuation. Further development
requires explicit owner authorization for a revised budget. This supersedes the
earlier 926,817 estimate, not any saved map checkpoint.

### Restart recovery / budget pause (supersedes older status below)

- Latest Blender master saved 2026-09-09 03:36 Berlin, 100 exported render groups / 192,820 triangles; includes 36 dimensional label meshes and the reseated blue banner.
- Latest completed Unity scene saved 03:36 Berlin: 64 renderers, 41 colliders, one lightmap atlas, 235 baked probes, no missing materials or wrong render layers. Five architectural UV warnings remain. Probe-lit details look too dark in the captured image; not visually approved.
- Latest label/banner export and targeted UV-padding fixes exist locally but have NOT been rebuilt into the saved Unity scene. Follow-up probe capture launch timed out connecting to the licensing service (exit 199); no new visual result was produced. This is not proof of an invalid license.
- The restart reset local token counters. Audit estimates 926,817 uncached-input-plus-output tokens combined across epochs; development is paused at the owner's 80% checkpoint. No new Unity or Blender build launched during recovery. Original 1M cap remains unchanged.
- Recovery checkpoint archives the existing Unity output and mirrors the saved build. It does not assert production completion or that pending source changes have passed validation.

Latest checkpoint (2026-09-09, release-work pass; supersedes counts below):

- 64 visible mesh groups / 172,412 evaluated triangles. Door glazing fixed on the two-car train; 83 recessed metal/caged fixtures replace the solid emissive boxes. Source geometry archived before each change.
- All 22 concourse piers reach their ceiling. Fifteen overhead beams have central clearance openings; gallery signs and the two end fixtures no longer obstruct the sampled upper route. No floor footprint or route layout changes.
- 41 separate unbeveled collider meshes / 12,656 triangles, layer 8, Metal/Cement surfaces, no triggers. Rendering also uses layer 8. Authoring TagManager is backed up before scoped additions.
- Unity point/capsule diagnostics: 20 of 24 points clear. Two north-platform samples intersect stair geometry, the centre-track sample intersects the train, and the old corridor marker (-13,-10) has no floor. These are NOT certified traversable routes. All six upper-route samples clear the 2 m hard standing limit; some remain below the 2.2 m recommendation.
- 24 Python tests pass. Replaying train, fixture and collision revision scripts creates no extra objects, meshes or collections. Winding/normal export checks pass.
- Connected UV2 topology and tile/metal surface-relief shaders compile in Unity. Corrective bake at 12 texels/m, 64 direct / 256 indirect samples is running with four-core affinity. First validation stage clears large architecture/train UV warnings but flags 42 small-detail groups. Probe-lit detail policy is authored locally and awaits the next deployment/bake. Do not confuse queued source changes with the currently baked scene.
- Real MapId requested from the owner; no substitute ID invented. Real retail SpawnPoint/MapConfiguration components are absent in LowerBay_Dev. No runtime-ready bundle or legacy export is certified.

The original lower entries are historical checkpoints, not current release claims.

- Token guard implemented and tested; original budget baseline retained.
- Gate 0: static reference ingestion complete. All contact sheets and supplementary chronological storyboards reviewed; continuous-motion review unavailable and not claimed. Reference notes saved under `reference/`.
- Owner's 2026-09-09 correction applied: Gemini output is excluded from all visual and geometry targets. Three walkthrough videos take priority.
- Gate 1: nine construction specs generated from the frozen seed and datasets; full oriented triangle conversion verified. Known layout/marker discrepancies are explicit, not silently corrected.
- Gate 2: isolated bootstrap and current-stage deterministic tools implemented and tested. Later-stage tools remain to be implemented gate-forward.
- Gate 3: exact rev4 blockout imported and saved; 60 groups, 15,372 source triangles, unchanged bounds.
- Gate 4: PARTIAL. Initial edge pass followed by revision 1: 36 full-height square tiled piers replace misplaced/fluted source columns, with original plan centres/envelopes preserved. 22 concourse piers untouched. Original column meshes archived, not deleted. Current visible geometry: 70,460 evaluated triangles. Full structure/clearance certification remains pending.
- Owner-requested geometry/material/Unity transition: two-car train refinement and intermediate Unity-2022 visual export now exist (54 groups, 89,992 triangles). Custom square-tile shader, reused metal/floor textures, UV0/UV2 and preview fixtures are integrated in `LowerBay_Dev/Assets/LowerBayAstra/LevelLowerBay_Preview.unity`. One diagnostic light bake completed, improving visibility, but UV overlaps were reported on 54 meshes. This does NOT certify Gates 4–10 complete. Final bake, colliders, gameplay wiring and dual-engine release exports remain pending. See `UNITY_PREVIEW_HANDOFF.md`.
- Existing reconstruction files and other maps remain untouched.

## Next checkpoint

Review seven route-marker diagnostics and remaining visual discrepancies, then complete structure before hero/detail passes. Square hall piers are corrected; train front, fanlight, wall corners and fixture integration remain priority work. Do not mistake this structure checkpoint for a final model.

Owner-requested local copy: `builds/11_lowerbay/`. `tools/publish_local_copy.py` copies this dedicated branch build there and backs up differing destination files without deletion. The branch/worktree remains the development authority; do not edit both copies independently.

## Evidence limitations

Saved Blender master: `blender/map_master.blend`. Shape-check renders: `screenshots/gate03_blockout.png` and `screenshots/gate04_structure_edges.png`. These use Workbench studio shading, not final game shaders/lightmaps. Named checkpoints preserve each step. All 18 unit tests passed; reopening the saved master and replaying completed scripts made no duplicates. Route-marker ray probes are diagnostic only, not swept-capsule gameplay validation. Blender's optional external thumbnail-cache writes were denied, but the actual map and PNG saves succeeded and the blend reopened successfully.

- 2026-09-08T23:47:25.466810+00:00: Pre-change checkpoint: `blender\checkpoints\gate02_before_bootstrap_20260908T234725_452743.blend`.

- 2026-09-08T23:47:25.502099+00:00: gate02_bootstrap: PASS isolated tooling/bootstrap; later scripts developed gate-forward; master saved; measurements `gate02_bootstrap.json`.

- 2026-09-08T23:47:49.670414+00:00: Pre-change checkpoint: `blender\checkpoints\gate03_before_import_20260908T234749_652469.blend`.

- 2026-09-08T23:47:49.819467+00:00: Prior master preserved as `blender\checkpoints\gate03_blockout_prior_master_20260908T234749_817238.blend`.

- 2026-09-08T23:47:49.854793+00:00: gate03_blockout: PASS exact rev4 import; gameplay clearance not yet certified; master saved; measurements `gate03_blockout.json`.

- 2026-09-08T23:48:14.184098+00:00: Pre-change checkpoint: `blender\checkpoints\gate04_before_structure_20260908T234814_164339.blend`.

- 2026-09-08T23:48:14.511491+00:00: Prior master preserved as `blender\checkpoints\gate04_structure_edges_prior_master_20260908T234814_509065.blend`.

- 2026-09-08T23:48:14.540360+00:00: gate04_structure_edges: PARTIAL structure edge refinement; full passage checks pending; master saved; measurements `gate04_structure_edges.json`.

- Interactive scene preserved before load: `blender\checkpoints\interactive_before_load_20260909T000112_499187.blend`.

- 2026-09-09T00:03:51.500833+00:00: Pre-change checkpoint: `blender\checkpoints\gate04_before_column_revision_20260909T000351_390674.blend`.

- 2026-09-09T00:03:51.762237+00:00: Prior master preserved as `blender\checkpoints\gate04_column_revision_prior_master_20260909T000351_760454.blend`.

- 2026-09-09T00:03:52.063761+00:00: gate04_column_revision: PARTIAL: full-height square piers corrected; other structure issues pending; master saved; measurements `gate04_column_revision.json`.

- 2026-09-09T00:05:48.407374+00:00: Pre-change checkpoint: `blender\checkpoints\interactive_before_final_save_20260909T000548_268738.blend`.

- 2026-09-09T00:05:48.519392+00:00: Interactive Lower Bay master saved with corrected columns and camera framing; local build copy refreshed separately.

- 2026-09-09T00:11:05.738026+00:00: Pre-change checkpoint: `blender\checkpoints\geometry_before_train_revision_20260909T001105_463620.blend`.

- 2026-09-09T00:11:05.783974+00:00: Prior master preserved as `blender\checkpoints\geometry_train_revision_prior_master_20260909T001105_782086.blend`.

- 2026-09-09T00:11:05.986948+00:00: geometry_train_revision: Train geometry checkpoint; full structure clearance still pending; master saved; measurements `geometry_train_revision.json`.

- 2026-09-09T00:33:09.125649+00:00: Pre-change checkpoint: `blender\checkpoints\interactive_before_final_save_20260909T003308_816128.blend`.

- 2026-09-09T00:33:09.236452+00:00: Interactive Lower Bay master saved with corrected columns and camera framing; local build copy refreshed separately.

- 2026-09-09T00:43:53.028133+00:00: Pre-change checkpoint: `blender\checkpoints\before_clearance_revision_20260909T004352_878245.blend`.

- 2026-09-09T00:43:53.063591+00:00: Prior master preserved as `blender\checkpoints\geometry_clearance_revision_prior_master_20260909T004353_061836.blend`.

- 2026-09-09T00:43:53.270764+00:00: geometry_clearance_revision: Construction corrections saved; capsule sweep still required; master saved; measurements `geometry_clearance_revision.json`.

- 2026-09-09T00:46:10.851118+00:00: Pre-change checkpoint: `blender\checkpoints\before_collision_proxies_20260909T004610_704651.blend`.

- 2026-09-09T00:46:10.857745+00:00: Prior master preserved as `blender\checkpoints\collision_proxies_prior_master_20260909T004610_855941.blend`.

- 2026-09-09T00:46:11.088706+00:00: collision_proxies: Proxy meshes generated; runtime capsule/route tests still required; master saved; measurements `collision_proxies.json`.

- 2026-09-09T00:57:15.066626+00:00: Pre-change checkpoint: `blender\checkpoints\geometry_before_train_revision_20260909T005714_934865.blend`.

- 2026-09-09T00:57:15.121988+00:00: Prior master preserved as `blender\checkpoints\geometry_train_revision_prior_master_20260909T005715_118262.blend`.

- 2026-09-09T00:57:15.331358+00:00: geometry_train_revision: Train geometry checkpoint; full structure clearance still pending; master saved; measurements `geometry_train_revision.json`.

- 2026-09-09T00:59:06.508238+00:00: Pre-change checkpoint: `blender\checkpoints\before_fixture_revision_20260909T005906_359939.blend`.

- 2026-09-09T00:59:06.552166+00:00: Prior master preserved as `blender\checkpoints\fixture_revision_prior_master_20260909T005906_550245.blend`.

- 2026-09-09T00:59:06.798531+00:00: fixture_revision: 83 recessed/caged fixtures; unchanged envelopes, source archived; master saved; measurements `fixture_revision.json`.

- 2026-09-09T00:59:08.930207+00:00: Pre-change checkpoint: `blender\checkpoints\before_collision_proxies_20260909T005908_822542.blend`.

- 2026-09-09T00:59:08.937324+00:00: Prior master preserved as `blender\checkpoints\collision_proxies_prior_master_20260909T005908_935513.blend`.

- 2026-09-09T00:59:09.160438+00:00: collision_proxies: Proxy meshes generated; runtime capsule/route tests still required; master saved; measurements `collision_proxies.json`.

- 2026-09-09T01:15:30.034105+00:00: Pre-change checkpoint: `blender\checkpoints\interactive_before_final_save_20260909T011529_843723.blend`.

- 2026-09-09T01:15:30.145709+00:00: Interactive Lower Bay master saved with corrected columns and camera framing; local build copy refreshed separately.

- 2026-09-09T01:29:08.970659+00:00: Pre-change checkpoint: `blender\checkpoints\before_station_labels_20260909T012908_743969.blend`.

- 2026-09-09T01:29:09.212208+00:00: Prior master preserved as `blender\checkpoints\station_labels_prior_master_20260909T012909_210280.blend`.

- 2026-09-09T01:29:09.450093+00:00: station_labels: Dimensional station/destination lettering; non-colliding detail only; master saved; measurements `station_labels.json`.

- 2026-09-09T01:36:43.148178+00:00: Pre-change checkpoint: `blender\checkpoints\before_blue_banner_seating_20260909T013643_015195.blend`.

- 2026-09-09T01:36:43.151911+00:00: Prior master preserved as `blender\checkpoints\blue_banner_seating_prior_master_20260909T013643_149964.blend`.

- 2026-09-09T01:36:43.377916+00:00: blue_banner_seating: Blue destination banner lowered beneath the hall ceiling; other banner already inside high spawn room; master saved; measurements `blue_banner_seating.json`.
