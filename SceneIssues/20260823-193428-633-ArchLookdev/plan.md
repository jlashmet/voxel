# SceneIssue 193428 — sky visible through arch wall

## Goal

Remove the seven marked see-through slivers in the intact ArchLookdev wall while preserving real
masonry joints, the arch opening, authored profile ownership, and authoritative voxel occupancy.

## Scope and constraints

- Replay the single saved 1637×1140 `Hero Arch Camera` pose at 34-degree FOV.
- Inspect all four crown/top-row and three right-shoulder marked regions.
- Treat occupancy as authoritative and obey the authored-boundary contract: sign matches occupancy,
  half-cell bias remains valid for flat bounds, and one presented surface has one owner.
- Distinguish intentional recessed mortar/joints from actual background-visible topology holes.
- Continue on `fixes`; use Unity only through `tools/unity-run.sh` and the production player.
- Record every experiment immediately; after three failed fix attempts, isolate a minimal repro.

## Acceptance criteria

1. Exact-pose replay confirms whether each marked sliver persists on current `fixes`.
2. Geometry/occupancy evidence identifies whether the hole originates in authoring, continuous
   topology, faceted masonry, retained profiles, or cross-path ownership.
3. A focused regression proves intact wall coverage without hiding valid joints or filling the arch.
4. The smallest causal fix closes all seven background-visible holes and preserves prior arch tests.
5. A clean final exact replay and affected Unity tests pass; fix/evidence and resolution metadata
   are committed separately and pushed.

## Work

- [x] Read the issue metadata, screenshot, all seven circles, and authored-boundary contract.
- [x] Replay the exact pose and classify the current marked slivers.
- [x] Isolate the responsible ownership/authoring invariant.
- [x] Add a focused regression and implement the proven fix.
- [x] Run affected tests and final exact replay.
- [ ] Review, commit/push, and resolve the manifest separately.

## Findings

- The stable holes expose the background at veneer/block boundaries seen obliquely; internal dark
  joints remain intentional.
- The front backing is an exact planar `FillIfEmpty` layer. Empty joint cells in front/adjacent can
  retain rounded veneer boundary halos.
- All three faceted-mask implementations suppress a solid planar face when the empty neighbour's
  boundary applies along that axis, even when the solid backing cell itself has no authored
  boundary on the face. The focused two-cell regression proves that face ownership is lost.
- Direction for attempt 1: the occupied cell's reconstruction and own boundary select continuous
  versus faceted ownership; an unrelated empty-cell halo must not erase an exact occupancy face.
