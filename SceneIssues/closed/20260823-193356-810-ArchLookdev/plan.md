# SceneIssue 193356 — missing ivy and flowers

## Goal

Restore the ivy and flower coverage expected from the ArchLookdev reference across all five marked
areas, without obscuring the arch construction or changing structural voxel behavior.

## Scope and constraints

- Replay the single saved 1637x1140 `Hero Arch Camera` pose at 34-degree FOV.
- Inspect every marked crown, shoulder, and right-pier region against the configured reference.
- Distinguish missing authoring/state from delayed generation, visibility, and renderer defects.
- Continue on `fixes`; use local Unity only through `tools/unity-run.sh` and the production player.
- Record every experiment and preserve exact replay evidence in this capture directory.

## Acceptance criteria

1. Exact-pose replay establishes current behavior at every marked region.
2. Reference comparison identifies the intended ivy/flower distribution rather than guessing.
3. A focused regression proves the responsible growth authoring/presentation invariant.
4. The smallest causal fix restores visible ivy and flowers without structural or coverage regressions.
5. A clean final exact replay and affected Unity tests pass; implementation/evidence and resolution
   metadata are committed separately and pushed.

## Work

- [x] Read the issue metadata, saved frame, all five circles, and ArchLookdev instructions.
- [x] Replay the exact pose and inspect current growth state.
- [x] Compare against the repository reference and isolate the responsible subsystem.
- [x] Add a focused regression that preserves the already-landed causal fix and its lifecycle.
- [x] Run affected tests and final exact replay.
- [x] Review, commit/push, and resolve the manifest separately.

## Findings

- Exact current-head replay at 24 seconds shows the intended growth in all five marked regions.
- The focused tracked target is `References/arch_reference.png`; it shows the intended dense
  left-pier/crown growth and sparse right-pier ivy directly. The broader source image remains at
  `References/sunlit-cleric-reference.png`; the older documented Downloads/Artifacts paths are
  absent.
- The broken capture predates `dde64c8fe` (`feat(showcase): match arch foliage presentation`) by
  about three minutes. That commit enlarged and added wall/crown flower heads; the preceding
  commits established dense left-side ivy and sparse right-side counterweight islands.
- This issue is vegetation authoring and production instanced rendering, not CPU/GPU Transvoxel.
- Direction change: preserve the existing visual fix, add the missing direct distribution/lifecycle
  regression, then validate and resolve the stale issue record. No further visual tuning is justified
  by the exact replay.
