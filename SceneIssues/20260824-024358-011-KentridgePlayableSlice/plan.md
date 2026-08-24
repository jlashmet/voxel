# SceneIssue 024358 — opening pub covered by terrain

## Goal

Make the opening cutscene visibly stage the cast inside the generated pub rather than presenting a
grass/terrain or upper-floor surface across the room. Fix the authoritative site preparation or
stage-to-building alignment if voxel evidence proves an overlap; do not disguise authoritative
solid terrain with a broader presentation cutaway.

## Scope and constraints

- Replay the saved 1637×1140 `Kentridge Player Camera` pose at 58-degree FOV and the captured
  opening-dialogue state.
- Inspect the single marked central region plus the full pub/actor framing around it.
- Preserve voxel occupancy as the authoritative source for rendering and collision. The opening
  cutaway remains presentation-only and bounded to the pub footprint, while any proven erroneous
  solid cells inside the authored pub must be corrected in deterministic CPU world generation.
- Distinguish stage-camera placement, floor altitude, pub realization, cutaway bounds, and renderer
  cutaway application before changing production code.
- Continue on `fixes`; use Unity only through `tools/unity-run.sh` and the production player.
- Record every experiment immediately; isolate a minimal repro after three failed fix attempts.

## Acceptance criteria

1. Exact-pose/current-opening replay establishes whether the marked grass plane persists.
2. Runtime and authored data identify which physical/presentation surface obscures the ground floor.
3. A focused regression proves the correct bounded opening visibility invariant.
4. The causal fix reveals the pub cast/interior without slicing actors, nearby terrain, or gameplay
   geometry outside the opening presentation.
5. A clean final opening replay and affected Unity tests pass; fix/evidence and resolution metadata
   are committed separately and pushed.

## Work

- [x] Read the issue metadata, screenshot/circle, opening camera code, cutaway implementation, and
  existing presentation regression.
- [x] Replay the exact saved pose and current authored opening.
- [x] Isolate camera, cutaway-bounds, stage alignment, or world-realization cause.
- [x] Add/extend a focused regression and implement the proven fix.
- [x] Run affected tests and final opening replay.
- [ ] Review, commit/push, and resolve the manifest separately.

## Findings

- The Kentridge-only catalogue leaves all four occupied stage capsules empty and puts foundation
  stone directly below them.
- Production humanoid renderer minima are within 3.2 cm of their feet roots, so visual pivots are
  not sinking the cast.
- The exact saved camera-to-Weldon torso ray hits authoritative material 6 at voxel
  `(1339,231,757)` below/outside the active roof cutaway. The complete production catalogue or
  camera relationship, not GPU extraction, is the remaining cause to isolate.
- Hightown alone introduces that material (`k=0`, `kh=6`, `kc=0`, `khc=6`). Its canonical pass
  incorrectly includes Kentridge-only absolute-placement stages; the direct contributor is
  `kentridge-working-lane-block-court`, and a catalogue-wide boundary test proves many sibling
  stages also leak south into Kentridge.
- The combined pass must retain its existing stage order for Kentridge, but Hightown may run only
  stages whose placements derive from `SettlementVoxelPlan`. Kentridge's district terraces,
  authored circulation/connectors, sidewalks, forecourt, street dressing, courts, vertical
  frontages/fabric/galleries, skybridge, access, hillside architecture, and undercrofts are
  Kentridge-owned content and will be gated together. The shared town-dressing adapter also needs
  to derive its elevation reference from the resolved settlement centre rather than Kentridge's
  fixed centre.
- Final validation: boundary regression 1/1; clean focused PlayMode regressions 2/2; full two-town
  fixture 9/9; Kentridge generation fixture 10/10. The production macOS player exited with zero
  assertion failures and `verification-fixed-pose-line-11.png` proves the original 1637×1140,
  58-degree-FOV Logan beat is clear in the GPU-rendered path.
