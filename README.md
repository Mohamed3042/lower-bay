# Lower Bay — Map v1

A work-in-progress reconstruction of **Lower Bay**, an unreleased 2013 UberStrike
multiplayer map. This repository is a **standalone v1 handoff** for design review and
continuation — the geometry, materials, lighting intent, and gameplay layout as they
stand today, with everything you need to open it, look at it, and keep building.

> **Status: intermediate visual candidate.** The layout, structure, and material/lighting
> pass are in place and video-verified for gameplay geometry. It is **not** a final,
> bake-certified, shippable Unity scene. Known open items are listed in
> [`docs/BUILD_STATE.md`](docs/BUILD_STATE.md) — read it before continuing the work.

---

## What Lower Bay is

An abandoned underground subway/metro station, named after Toronto's real "Lower Bay"
ghost station. Originally a Cmune "bluebox" community-preview map from the **UberStrike
4.3.x era (spring 2013)**, cut before release. The original map file is lost; three
walkthrough videos are the surviving reference (see [`reference/`](reference/)).

- **Mood:** dim, grimy, fluorescent-lit, graffiti-covered — atmospheric decay. Cold
  grey/white tile, warm fluorescent pools, red/magenta signage accents. Arched-window
  daylight at the hall ends is the primary light source.
- **Shape:** a single two-car train down a central track; a glass-sided concourse on one
  flank, a solid-walled corridor ring on the other, with elevated spawn mezzanines at
  each end.
- **Mode:** Team Death Match (2 spawns — blue west / red east).

### Key numbers

| | |
|---|---|
| Scene name | `LevelLowerBay` |
| Footprint (Unity units ≈ metres) | ~92 × 29 × 11 (X × Z × Y) |
| Vertical levels | trench −1.2 · platform 0 · train roof 2.2 · catwalk 3.0 · spawn deck 3.4 |
| Zones | 8 (station hall, track trench, concourse, corridor ring, vending alcove, catwalks, 2 spawns) |
| Spawns / pickups | 2 / 11 |

Full locked reference: [`reference/LOWER_BAY_CANONICAL_FACTS.md`](reference/LOWER_BAY_CANONICAL_FACTS.md).
Machine-readable layout: [`spec/`](spec/).

---

## Open it

### Unity (recommended — see the built map)
1. Unity (project was authored against a legacy forward pipeline; any recent LTS opens the package).
2. `Assets → Import Package → Custom Package…` → `export/unitypackage/LowerBay_v1.unitypackage`.
3. Import all. The map assets land under `Assets/LowerBayAstra/`. Open the imported scene.
4. Legacy shaders and material/lighting helper scripts are in [`export/unity/`](export/unity/)
   if you need to rebuild materials.

### Blender (edit the source geometry)
- Open `blender/map_master.blend`, scene **`LevelLowerBay`**. Authored in Blender 5.2.
- The build pipeline that produced it is in [`blender/scripts/`](blender/scripts/) (blockout →
  structure → revisions → collision → preview export), runnable headless.

### Just look (no software)
- [`preview/`](preview/) — rendered checkpoints of the build (blockout, structure, columns,
  train, labels, final geometry).
- [`reference/`](reference/) — concept storyboards from the source footage.

---

## Repository layout

```
blender/        map_master.blend + the headless build pipeline (scripts/)
export/
  unitypackage/ LowerBay_v1.unitypackage    <- drop into Unity
  unity/        legacy shaders + material/lighting/repair helper scripts
spec/           machine-readable design spec (layout, zones, routes, spawns,
                pickups, materials, lighting, validation rules) — start here for AI
reference/      canonical facts sheet, concept storyboards, source seed geometry (.obj/.forge)
preview/        rendered screenshots of each build gate
docs/           build state / known issues, re-audit findings, source manifest
```

## Continuing the work (for an AI agent or designer)

The design intent is **authoritative in this order**: the three reference videos → the
concept storyboards in `reference/` → `reference/LOWER_BAY_CANONICAL_FACTS.md` → `spec/`.
Where a number conflicts with the footage, the footage wins — but keep the **gameplay
geometry** (spawns, item positions, catwalk drop-points, sightline spine) intact; that
layer is already video-verified.

Before extending it, read:
- [`docs/BUILD_STATE.md`](docs/BUILD_STATE.md) — what's done and what's open.
- [`docs/STRUCTURAL_CORRECTION_FINDINGS.md`](docs/STRUCTURAL_CORRECTION_FINDINGS.md) and
  [`docs/OWNER_REJECTION_AND_REAUDIT.md`](docs/OWNER_REJECTION_AND_REAUDIT.md) — corrections
  already made and why, so they aren't undone.
- [`spec/validation_rules.json`](spec/validation_rules.json) — the checks the build must pass.

## Notes

- Unity units are treated as metres. Up axis: **Y** in Unity, **Z** in Blender.
- `LowerBayAstra` is the internal asset/codename for this build; asset paths and script
  class names use it — leave it in place so the package and helper scripts stay linked.
- This is a curated handoff copy: session/build tooling, backups, and heavy intermediate
  data dumps from the working tree are intentionally excluded.
