# Plan — prevent Kentridge entrance/window overlap

## Goal

Fix `20260824-220416-979-VoxelShowcase`: Medrare House reads as though its public entrance and a frontage window are merged. Generated Kentridge facades must preserve a visible wall gap between the door opening and frontage-window openings without deleting the architecture-owned frontage rhythm.

## Final diagnosis

The live generated-house path is:

`KentridgeSharedStructureVoxelCatalogue` → `KentridgeSharedHouseProgram.Build` → `HouseProgramCompiler.BuildProgram`.

`StructureForm` owns the Kentridge `FrontageRhythm` and `DoorOffsetDm`, but before the fix the shared-house adapter only translated the door offset. Front windows continued to use the selected generic house preset's count/placement. That allowed an off-centre Kentridge entrance and a preset-owned frontage window to occupy the same facade region.

The earlier production change in `KentridgeGrammarVoxelCatalogue` targeted a superseded Kentridge geometry path and therefore could not fix the live VoxelShowcase output. The focused regression also initially searched for filled glazing/detail boxes; the shared house compiler represents facade openings as `ShapeOp.EmitBox` carve operations.

The live-path fix is `cd4480b134461b1eddb33a05c78735e4489bf4f5`. It translates Kentridge's architecture-owned frontage rhythm to explicit offsets in `KentridgeSharedHouseProgram`, retaining the selected shared-house preset's window opening dimensions while deterministically reflowing bays around the public door and one another.

Standalone saved-view replay plumbing is now reusable: `SceneIssueCapture` consumes the opt-in `-voxel-scene-issue` argument in development players and delegates to the same replay path used by editor replay, while `tools/showcase-player-capture.sh --scene-issue` builds a development player and forwards the issue path without changing default capture behavior.

The first strict current-head replay attempt, Actions run `32830868865`, failed during the Unity player build before launch and was recorded as inconclusive in `experiment-007-current-head-replay-verifier.md`. After adding failure diagnostics without changing geometry or replay semantics, run `32831102139` succeeded from source `86cddfd2ce219256cc86ea6e85760dd80e5a9332`: `SceneIssueReplayVerification` logged `Verified standalone frozen pose`, and the final exact-view render shows a distinct masonry pier between the public entrance and left frontage window while retaining the right frontage window. The successful replay is recorded in `experiment-008-current-head-replay-success.md`.

A final net-diff review against pre-issue baseline `a2c8ab9427ed245450099d06f4768b4b1c2cf922` confirmed that the failed legacy `KentridgeGrammarVoxelCatalogue` change is absent and the repurposed one-shot workflow has been restored byte-for-byte to its pre-issue contents. The remaining production geometry change is the live shared-house implementation plus its regression; reusable scene-replay tooling and issue evidence remain intentionally.

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
- [x] Run a current-head standalone replay and require `SceneIssueReplayVerification` to confirm the recorded camera position, rotation, and FOV were reached (`32831102139`).
- [x] Record the fresh replay experiment and retain its screenshot/log evidence with the issue (`experiment-008-current-head-replay-success.md`, artifact `9556851106`).
- [x] Review the final net diff against the pre-issue baseline and remove obsolete 220416 one-shot CI wiring.
- [x] Produce/review the fresh VoxelShowcase replay render and visually confirm the facade is no longer merged.
- [x] Update `issue.json` as fixed in a separate bookkeeping commit (`aafd9e8382a444ce537990b09e55883f412eb948`).

## Production attempts

1. `1f8b92d00ec2e286379b153ab0828c977c498248` — reflowed windows in `KentridgeGrammarVoxelCatalogue`; later proven to target the superseded path and removed from the final diff.
2. `cd4480b134461b1eddb33a05c78735e4489bf4f5` — moved the invariant to the live shared-house adapter, translated Kentridge frontage rhythm to explicit shared-house offsets, corrected the bytecode regression, and reverted the dead-path change.

Three production attempts is the escalation threshold; the accepted live-path implementation is attempt 2. Replay/capture tooling changes are validation infrastructure, not additional production geometry attempts.

## Acceptance

- [x] `MedrareHouseKeepsBothFrontageWindowsClearOfDoor` executes at least one test and passes in Unity CI.
- [x] The emitted Medrare program contains exactly two front-window openings for its asymmetric frontage.
- [x] Each front-window opening is separated from the physical public door by at least 3 dm.
- [x] The published door anchor identifies that same physical door opening.
- [x] Existing affected Kentridge architecture/worldgen tests remain green.
- [x] Final source contains one active implementation of the invariant, not parallel legacy/shared fixes.
- [x] Standalone replay consumes the saved issue record and uses the same frozen camera/anchor semantics as editor replay.
- [x] With no replay argument, standalone capture behavior is unchanged.
- [x] A fresh current-head VoxelShowcase replay of the reported camera/scene reaches the recorded frozen pose and no longer shows a merged door/window facade.

Issue `20260824-220416-979-VoxelShowcase` is resolved and all acceptance criteria are satisfied.
