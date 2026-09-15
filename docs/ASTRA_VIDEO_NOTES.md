# Lower Bay — footage observations (work in progress)

Source folder: `<local>/footage_frames`.
See `REFERENCE_PRIORITY.md` for the owner override and coverage limitations. These are observations of extracted frames, not claims of continuous video playback or measured dimensions.

## Clip 2 — Stratos, sheets 01–14

- 00:00–00:37.5: red transit banner, bright rectangular train windows, broad concourse with low flat ceiling.
- 00:38–00:45.5: framed glass partition; square tiled piers with luminous strips near their tops; square wall tiles and dark lower dado; curved ramp toe. Blue LowerBay / Gideons Tower and red Fort Winter direction signs.
- 00:46–01:03.5: rounded tiled wall junctions, BMX graffiti, recessed service door, green bins and warm local wall-light pools. Routes remain readable rather than uniformly dark.
- 01:04–01:12.5: train end and safety stripe; low rectangular passage leading to a ramp; upper grated gantry, slim side rails, far-end fanlight and closed door.
- 01:13–01:29: dark door recess and return to concourse. Scoreboard identifies LowerBay and Team DeathMatch; this does not establish dimensions.
- 01:29.5–01:48.5: red LowerBay / Fort Winter direction banner with white train icon, Christmas-themed graffiti.

## Clip 3 — wsdea, sheets 01–25

- 00:00.5–00:26: intro/lobby; little geometry evidence.
- 00:27–00:32.5: wide concourse, low flat ceiling, large advertisements, tiled pier with bright trim near top, low opening to ramp.
- 00:33–00:43.5: upper gantry, thin railings, fanlight, train and structural roof supports.
- 00:44–01:05: ramp/corridor, dark dado, rounded corners, green bin, small service cabinet, blue and red LowerBay direction signs with white train icon.
- 01:05.5–01:20: wall flyers and caged lamps. Green wheeled bin has a hinged lid and modeled base; loose paper is visible. Motion is not established by these stills.
- 01:20.5–01:38.5: recessed closed door, curved ramp foot, broad concourse; wood-slatted bench, bin, Christmas graffiti and blue-jacket advertisement.
- 01:39–01:56.5: Godfather's Suit Jacket advertisement; adjacent faded billboard; yellow TITO SANCHEZ graffiti; two vending machines with recessed product display and lower pickup slots; round caged wall light.
- 01:57–02:05.5: glass partition, large advertisements, small round lamps, low ramp passage, blue transit banner and bright cream-white train windows.
- 02:06–02:19: ribbed aluminum train body, door/window recesses, paired rails and curved tunnel transition into darkness/fog rather than an exposed outside void. Train underside needs actual modeled support/wheel forms.

## Construction implications supported by footage

### Completed static evidence pass, 2026-09-09

Clip 3 sheets 26–32: train front has three tall window/door divisions, round lower lamps, recessed undercarriage; rail sleepers and papers are visible from trench level. End fanlight has radial divisions and a barred lower opening. Slender gantry posts run beside the track. The final sequence shows the player outside/below visible geometry: this is a defect to prevent, not a route to copy.

Clip 1 was decoded once to `clip1_storyboard.png` at 3-second intervals (30 frames, chronological row-major). This independently corroborates square tiled walls, rounded corridor corners, flat weathered ceilings, strip-lit square piers, blue/red direction signs, framed advertisements, luminous train windows and the ramp-to-gantry connection. This sample is not a continuous-motion review.

Clip 2's previously uncovered tail was decoded once to `clip2_tail_storyboard.png`, beginning at 109 seconds, sampled every 4 seconds. It stays on a red direction sign and tiled wall while chat continues; no new geometry target was observed.

All available contact sheets for clips 2 and 3 are now reviewed. Static reference ingestion is sufficient for importing the supplied frozen blockout and a non-layout-changing edge pass. Continuous-motion review remains unavailable; no claims of exact measured video dimensions or motion validation are made.

- Prioritize recognizable train profiles, framed openings, rounded tiled junctions, concourse piers, slim gantry rails, and correctly mounted lamps.
- Use square off-white tile, dark dado, restrained metal wear, readable signage and localized warm light. Do not borrow Gemini's extra tracks, altered floor plan, broken catwalks, crowds or invented advertisements.
- Keep uncertainty explicit: no metric sizes derived from perspective stills; sign spelling and exact asset details require sufficiently clear frames.
- Bake only after geometry, UVs, normals and source placement are stable. Visibility/readability is an acceptance requirement, not simply increased exposure.

## Seed handling already established

The rev4 OBJ is right-handed Y-up in metres. Its sibling forge JSON has already mirrored X and reversed triangle winding for Unity. Never mirror that JSON a second time. Rev4 metadata contains 23 tags and 34 materials, despite the handoff's 24-tag claim. These are data handling facts, not confirmation that the seed matches every video detail.
