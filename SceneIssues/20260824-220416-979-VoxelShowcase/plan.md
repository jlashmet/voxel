# Plan — prevent Kentridge entrance/window overlap

## Goal

Fix `20260824-220416-979-VoxelShowcase`: Medrare House reads as though its public entrance and a frontage window are merged. Generated Kentridge facades must preserve a visible wall gap between the door opening and frontage-window openings without deleting the architecture-owned frontage rhythm.

## Current diagnosis

The live generated-house path is:

`KentridgeSharedStructureVoxelCatalogue` → `KentridgeSharedHouseProgram.Build` → `HouseProgramCompiler.BuildProgram`.

`StructureForm` owns the Kentridge `FrontageRhythm` and `DoorOffsetDm`, but before the current fix the shared-house adapter only translated the door offset. Front windows continued to use the selected generic house preset's count/placement. That meant an off-centre Kentridge entrance and a preset-owned frontage window could occupy the same facade region.

The earlier production change in `KentridgeGrammarVoxelCatalogue` targeted a superseded Kentridge geometry path and therefore could not fix the live VoxelShowcase output. The focused regression also initially searched for filled glazing/detail boxes; the shared house compiler represents facade openings as `ShapeOp.EmitBox` carve operations.

The production geometry fix and its affected Kentridge entrance-fixture validation are green. Standalone saved-view replay plumbing is also now implemented: `SceneIssueCapture` consumes the opt-in `-voxel-scene-issue` argument in development players and delegates to the same replay path used by editor replay, while `tools/showcase-player-capture.sh --scene-issue` builds a development player and forwards the issue path without changing its default behavior.

A successful standalone replay artifact was captured by Actions run `32828066040` from source `550cee76ab9bcb3ad5b4ea6aeceecd89a29a4480`; the output visibly contains the scene-issue replay banner/circle at the saved view. However, the explicit `SceneIssueReplayVerification` frozen-pose observer was added later at `46db0f71fd4fb328f9db951ebf276bc1d98a3dd0`, so that earlier green run cannot prove the verifier itself executed. The next validation must run from the current head and require the verifier's success log before the issue can be closed.

## Intended invariant

- Kentridge remains the owner of frontage rhythm and door placement.
- The shared house preset remains the owner of the window opening's dimensions/style/material policy.
- The Kentridge adapter converts `FrontageRhythm` to explicit front-window offsets before invoking the generic `HouseProgramCompiler`.
- Every front window keeps at least 3 dm of visible facade from the public door opening.
- Front windows also keep at least 3 dm from one another and remain inside a 6 dm facade side margin.
- Collision handling reflows a bay to the nearest deterministic legal position instead of silently dropping the bay.
- No captured world coordinate or Medrare-specific special case is introduced.
- Scene replay uses the existing `SceneIssueCapture` frozen-pose implementation; standalone capture plumbing must not alter ordinary VoxelShowcase camera behavior when no replay argument is supplied.

## Work / validation

- [x] Reproduce the issue structurally with Medrare House's deterministic asymmetric frontage and `DoorOffsetDm = -8`.
- [x] Discover the live shared-house compilation path and distinguish it from the legacy grammar path.
- [x] Remove the ineffective legacy-path entrance/window reflow change.
- [x] Implement architecture-owned frontage placement in `KentridgeSharedHouseProgram` using shared `ExplicitOffsets`.
- [x] Correct the regression to inspect emitted front-wall carve openings and verify the physical door against the published door anchor.
- [x] Run the focused EditMode regression through `ci-test/fixes` and require `ci/single-test` success (`32819100852`, 1 test, 63 s).
- [x] Run broader affected Kentridge/worldgen validation (`experiment-006-entrance-fixture-ci.md`: Pub alignment + Medrare clearance both passed).
- [x] Add opt-in development-player scene-issue replay input that delegates to the existing frozen replay behavior.
- [x] Thread the issue path through `tools/showcase-player-capture.sh` without changing its default behavior.
- [ ] Run a current-head standalone replay and require `SceneIssueReplayVerification` to confirm the recorded camera position, rotation, and FOV were reached.
- [ ] Record the fresh replay experiment and retain its screenshot/log evidence with the issue.
- [ ] Review the final net diff against the pre-issue baseline and remove obsolete one-shot CI wiring.
- [ ] Produce/review the fresh VoxelShowcase replay render and visually confirm the facade is no longer merged.
- [ ] Only after CI and fresh visual replay, update `issue.json` as fixed and record the final commit/evidence.

## Production attempts

1. `1f8b92d00ec2e286379b153ab0828c977c498248` — reflowed windows in `KentridgeGrammarVoxelCatalogue`; later proven to target the superseded path.
2. `cd4480b134461b1eddb33a05c78735e4489bf4f5` — moved the invariant to the live shared-house adapter, translated Kentridge frontage rhythm to explicit shared-house offsets, corrected the bytecode regression, and reverted the dead-path change.

Three production attempts is the escalation threshold; the current live-path implementation is attempt 2. Replay/capture tooling changes are validation infrastructure, not additional production geometry attempts.

## Acceptance

- `MedrareHouseKeepsBothFrontageWindowsClearOfDoor` executes at least one test and passes in Unity CI.
- The emitted Medrare program contains exactly two front-window openings for its asymmetric frontage.
- Each front-window opening is separated from the physical public door by at least 3 dm.
- The published door anchor identifies that same physical door opening.
- Existing affected Kentridge architecture/worldgen tests remain green.
- Final source contains one active implementation of the invariant, not parallel legacy/shared fixes.
- Standalone replay consumes the saved issue record and uses the same frozen camera/anchor semantics as editor replay.
- With no replay argument, standalone capture behavior is unchanged.
- A fresh current-head VoxelShowcase replay of the reported camera/scene reaches the recorded frozen pose and no longer shows a merged door/window facade.

The issue remains open until the fresh replay requirement is satisfied; structural and CI evidence alone is not labelled visual verification.
