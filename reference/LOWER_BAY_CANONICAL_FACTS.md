# LOWER BAY — Canonical Facts Sheet (locked layout & identity)

> **What this is:** the single distilled reference of every *locked* number and identity detail for the
> unreleased 2013 UberStrike map **"Lower Bay"**, extracted from the rev3 locked blockout
> (`_source/geometry.py`), `_source/spec.py`, and `_source/LOWER_BAY_MAP_BIBLE.md`.
>
> **How to use it (GPT‑6 Astra):** this is your **cross‑check**, not your source. The **3 videos are the
> primary ground truth** (see `GPT6_ASTRA_START_HERE.md`). Where a number here conflicts with what you
> see in the footage, **the footage wins** — but flag the conflict and keep the *gameplay geometry*
> (spawns, item positions, catwalk drop‑points, sightline spine) intact, because that layer was already
> video‑verified (Gemini watched the clips and corrected it in rev3).

---

## 0. Identity

- **Name:** Lower Bay. Abandoned underground subway/metro station, named after Toronto's real "Lower Bay"
  ghost station. On‑screen scoreboard in the clips: *Map: Lower Bay · Mode: Team Death Match · Server:
  [Cmune] Dev Game Server*.
- **Origin:** a Cmune "bluebox" community‑preview map from the **UberStrike 4.3.x era, late Apr–early May
  2013**, **cut before release**. The map file is lost; the 3 walkthrough videos are the only surviving
  artifact.
- **Mood:** dim, grimy, fluorescent‑lit, graffiti‑covered, atmospheric decay. Cold grey/white tile +
  warm fluorescent pools + red/magenta signage accents. Arched‑window daylight at the hall ends is the
  primary light source.

## 1. Coordinate convention (rev3 source frame)

- Units **metres, right‑handed, Y up**. Datum **y=0 = platform / concourse / corridor floor**.
- **X = long axis** (east +). **Z = cross axis.** Key asymmetry along Z:
  - **+Z side = GLASS wall → wide CONCOURSE** (the main mid‑range arena).
  - **−Z side = SOLID tiled wall → tight CORRIDOR ring + Ad/Vending room** (CQC).
- **Axis‑label wart (do not be misled):** the source header text says "Z south(+)/north(−)" but the
  numeric layout is unambiguous — glass/concourse is at **+Z**. The `PLAT_N` name is on the +Z platform.
- **Unity export flips X** (left‑handed): source `x → Unity −x`, so east/west appear mirrored in the
  Unity scene (spawn A authored at −36 shows at +36 in‑engine). Unity frame = **+X east, +Y up, −Z north**.

## 2. Bounds & heights (rev3)

| Constant | Value | Meaning |
|---|---|---|
| `X0,X1` | −40, +40 | full extent incl. spawn boxes (80 m) |
| `HX0,HX1` | −38, +38 | hall / platform / track length = **76 m** |
| `TRACK_Y` | −1.2 | trench floor |
| `PLAT_Y` | 0.0 | platform / concourse / corridor datum |
| `CAT_Y` | 3.0 | catwalk deck |
| `SPAWN_Y` | 3.4 | spawn mezzanine deck |
| `HALL_CEIL` | 5.5 | arched hall crown (≈6.6 above trench) |
| `LOW_CEIL` | 3.9 | concourse ceiling |
| `CORR_CEIL` | 3.5 | corridor ceiling |
| `WALL` | 0.6 | wall thickness |

**Cross‑section along Z (single narrow track):**
`TRENCH = (−2.0,+2.0)` 4 m · track centreline `TRK = 0.0` · **PLAT_S = (−6.0,−2.0)** south platform (4 m,
borders solid wall) · **PLAT_N = (+2.0,+6.0)** north platform (4 m, borders glass wall) · glass wall
`GLASS_Z=+6.0` · solid wall `SOLID_Z=−6.0`.

**Rooms:** `CONC = (−38, 6.0, 38, 16.0)` concourse **76×10 m**, ceil 3.9 · `CORR = (−38, −8.5, 38, −6.0)`
corridor **76×2.5 m**, ceil 3.5 · `ADV = (−7.0, −12.0, 7.0, −8.5)` ad/vending alcove **14×3.5 m** (centre
of the south bracket).

**Spawn boxes (deck top y=3.4):** `SP_W = (−40,−4,−32,4)` 8×8 m west · `SP_E = (32,−4,40,4)` 8×8 m east.

## 3. Structures (ordered, as rev3 `build()` creates them)

1. **Floors** — platform slabs (PLAT_S/PLAT_N, top y=0, 0.4 thick); **yellow safety‑line** strips 0.35 w
   × 0.03 h at each platform lip (z=+2.0 and z=−2.0); concourse, corridor, ad‑alcove slabs.
2. **Single‑track trench** — ballast X −38..38 / Z −2..2, top y=−1.2; **two rails** at z=±0.72 (0.12
   w/h); retaining walls at z=−2.0 and z=+1.7, height 1.1.
3. **Hall walls (ASYMMETRIC — the map's signature):** north **GLASS** wall z=+6.0 (h 0..5.5) with **5
   doorways** to the concourse (openings at t=0.12/0.30/0.48/0.66/0.84, each 0..2.4 tall) + 6 mullions.
   South **SOLID tiled** wall z=−6.0 with **only 2 doorways** at the ends (t=0.05–0.11 and 0.83–0.89) —
   "corridor bracket enters at the ends, not the middle" — + a 0..0.9 dado band.
4. **Arched ends** at x=−38 and x=+38 — tile arch wall (Z −6..+6, h 0..5.5) with a single central **track
   portal** (t 0.42–0.58, 0..2.6) + a glowing **semicircular fanlight** disc (radius 2.0 at y=2.6).
5. **Hall roof** at y=5.5 (0.5 thick) + **13 ceiling lamp strips** at x=−35+k·5.6 (k=0..12), y≈5.18.
6. **Platform pillars** — round fluted columns r=0.55, y 0..5.5, at **xs = −33,−28,−23,−18,−13,−8,−3,2,
   7,12,17,22,27,32** (**14 × 2 rows = 28**), ~5 m spacing, z=−4.9 (south) & z=+4.9 (north); capital
   (1.4²×0.7) + base (1.3²×0.32) each.
7. **Concourse** — tiled N/E/W walls (S is the glass wall); **square‑pillar grid x=−30,−24…30 (11) × z=9.0
   & 13.0 (2) = 22 pillars**, side 0.9, y 0..3.9; roof 3.9 + 11 lamps.
8. **Corridor ring + ad alcove** — tiled walls; roof 3.5 + 13 lamps; **2 billboards** at x=−3.5 & +3.5 on
   the ADV front wall (z≈−11.69), 3.6 w × 2.4 h, y=0.9; vending at (5.8, z=−10.25); bench at (−5.2, z=−9.1).
9. **Elevated spawn rooms (y=3.4) + ramps** — each spawn deck + 3 walls (to y=7.3); **ramp descends
   y3.4→0 over z=4..12**; west ramp x=−35.2..−32, east ramp x=32..35.2; railing each.
10. **Longitudinal dead‑end catwalks** (deck y=3.0, width 2.6, z −1.3..+1.3, 0.14 thick) — **West
    x=−32..−18, East x=+18..+32** (each **14 m ≈ 0.78 car‑length**); railings + posts every 4 m; **inner
    end (x=∓18) is OPEN = a "diving board" drop onto the train roof / tracks.** ← key vertical mechanic.
11. **Trench end‑stairs** — at x=−35 and x=+32, width 2.2, rise y=−1.2 +1.6; a +z flight to north platform
    (z=1.9) and a −z flight to south platform (z=−1.9), near the arched portals.
12. **Rolling stock: ONE train = 2 cars of 18 m, parked dead‑centre** — cars at x=−18 (−18..0) and x=0
    (0..+18), on TRK=0, width 2.9, body y=−0.85; **train spans X −18..+18 (36 m), centred.**
13. **Furniture** — 6 platform benches; 4 green bins; platform signs at x=−24,0,24 (blue on north lip,
    red on south lip); **transit banners** (red west x=−38, blue east x=24) — destination banners, *not*
    team markers; 14 lamp posts.
14. **Station detail** — fare‑gate row z=12.5 (**12 turnstiles** x=−11..11 step 2 + 2 end posts x=±13.2);
    **4 ticket machines** x=−21..−16.5 step 1.5, z=15.2; **info booth** (7,·,13.6) 3.4×2.6×2.4 + canopy;
    **restroom block** NE corner X 24..31 / Z 11.5..15.5, h 2.6; **service/electrical annexes** x=±36.5,
    z=−9.2.

## 4. Zones (8)

| id | name | level | role |
|---|---|---|---|
| H1 | Main Station Hall | L0 (y0) | hero space — tracks/trench + a platform each side, arched windows, train. LONG lane. |
| T1 | Track Trench | L−1 (y−1.1) | sunken spine under the platforms. |
| C1 | Concourse / Undercroft | L0 | **primary arena**, glass wall to hall, 22‑pillar grid, fare gates. MID. |
| R1 | Corridor Ring | L0 | tight flank behind the solid wall. CLOSE. |
| V1 | Ad & Vending Room | L0 | central CQC alcove, backlit billboards, heavy‑armour. CLOSE. |
| K1 | Catwalks | L1 (y3) | two dead‑end gantries over the track (sniper lane / diving boards). LONG. |
| SW | West Spawn mezzanine | L1 (y3) | x≈−36, elevated. |
| SE | East Spawn mezzanine | L1 (y3) | x≈+36, elevated. |

## 5. Gameplay (TDM, ~6v6) — LOCKED, video‑verified

- **Spawns (2):** `A COBALT (−36.0, 3.0, 0.0)` #3f7fe2 (blue, **west**); `B VERMILION (+36.0, 3.0, 0.0)`
  #e2603f (red, **east**). Ends 76 m apart, ~72 m spawn‑to‑spawn. Each spawn drops in via a ramp to the
  concourse (z 4..12) and a catwalk mouth over the track. *(Unity mirrors X.)*
- **Combat range tiers:** **Long** = central hall/track spine + catwalks (sniper lane, backlit arch ends).
  **Mid** = wide concourse (glass wall, pillar cover, fare gates) — main fight. **Close/CQC** = corridor
  ring + the central Ad & Vending room.
- **Items (11) — competitive economy:**
  | item | pos (x,y,z) | placement rationale |
  |---|---|---|
  | **SNIPER** (power) | (0, 2.2, 0) | **on the train roof, map centre** — reachable only by a catwalk diving‑board drop; committed/exposed grab hands over the long lane |
  | **HEAVY ARMOUR** | (0, 0, −10) | Ad room CQC, deliberately off the sniper sightlines |
  | ARMOUR | (0, 0, +11) | concourse centre — lateral pull across the glass wall |
  | health | (−28,0,8) & (+28,0,8) | spawn‑ramp bases — safe retreat |
  | health | (−25,−1.2,0) & (+25,−1.2,0) | trench under the catwalks — high‑risk |
  | ammo | (−30,0,−7) & (+30,0,−7) | corridor W/E |
  | ammo | (−8,0,+4) | north platform behind a pillar |
  | ammo | (+8,0,−4) | south platform |
- **Routes (7 polylines):** concourse, north_platform, south_platform, corridor, catwalk_w, catwalk_e,
  track. Shape = long central lane + flanking ring + vertical catwalks that dead‑end over the train.
- **POV cameras (8, for QA vs footage):** 1 north platform down the hall (58°) · 2 west catwalk
  diving‑board over train roof (60°) · 3 track trench (56°) · 4 concourse/fare‑gates (66°) · 5 ad &
  vending CQC (58°) · 6 west spawn mezzanine + ramp (66°) · 7 north platform mid (58°) · 8 corridor ring
  (62°).

## 6. Identity details — reproduce these EXACTLY

- **Signage (exact text):** blue **"LowerBay → Gideons Tower"** · red/magenta **"LowerBay → Fort Winter"**
  (with a train glyph) · big red overhead spawn banner **"LowerBay → Fort Winter / Gideons Tower"**.
- **Station network destinations:** **Fort Winter** and **Gideons Tower** (both are other UberStrike maps).
  Wider network also referenced: Port Whatire, Gulzar's Tower.
- **Graffiti / decals (exact):** BMX‑kid stencil, **"FAZE"**, **"TITO SANCHEZ"**, red **"DA CHRISTMAS"**,
  assorted tags; missing‑person flyer clusters. *(Prior recon also read "F.A.I.L", "PARADISE CITY"
  heart+palms, a rapper portrait — treat spelling as video‑confirm‑on‑sight.)*
- **Backlit ad billboards (named):** **"Godfather's Suit Jacket"**, **"Cuberstrike"**, a scenic, a
  wookiee. Cold pale‑blue backlight = a key light source in the ad room.
- **Materials palette:** grimy white **square** subway wall tiles (dominant) · concrete floors / pillars /
  trench / ceiling · fluorescent strip + round ceiling lamps · grey subway cars (lit windows/doors) ·
  metal railings / chain gates · arched fanlight glass · yellow platform safety line · near‑black
  skirting/dado on the walls.

## 7. Version drift — READ BEFORE TRUSTING ANY ONE FILE

Three artifacts exist and they do **not** match. Reconcile against the footage:

| artifact | rev | tris | X extent | mats | tags | notes |
|---|---|---|---|---|---|---|
| `LowerBay_Forge/_source/geometry.py` | **rev3** | runtime | ±40 | 21 | 19 | the **readable, annotated locked layout** (this sheet) |
| `LowerBay_Forge/UNITY/.../11_LowerBay.forge.json`+`.obj` | **rev4** | **15,372** | ±46 | 34 | 24 | **richest/newest**; adds tunnel portals (~6 m past each arch), ceiling beams, graffiti planes — but **its generating source is MISSING** |
| `LowerBay_Dev/.../11_LowerBay.forge.json` | earlier | 6,512 | ±40.3 | 21 | 20 | the textured/baked Unity blockout |

The rev3 `geometry.py` **cannot** reproduce the rev4 mesh (no portals/beams/graffiti, stops at ±40). Use
**rev3 as the intent spec**, **rev4 `.obj` as the richest geometry reference**, and **the videos as the
tiebreaker** for anything they disagree on.
