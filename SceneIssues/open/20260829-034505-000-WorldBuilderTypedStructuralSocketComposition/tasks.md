# Tasks

## Canonical production path / established regressions
- [x] Resume `fixes/agent-5`, follow `AGENTS.md` / feature/common SceneIssues rules, keep the assignment isolated, and use only `ci-test/fixes/agent-5` for targeted CI.
- [x] Consolidate structural composition on `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue`; no second structural solver.
- [x] Keep stable socket identity, semantic role/tags, facing, integer transforms, clearance, cardinality/capacity, support probes, required/optional behavior, graph diagnostics, and deterministic hashing data-driven/inspectable.
- [x] Enforce deterministic child selection plus compatibility, orientation, clearance/overlap, spacing, support, capacity, recursion/depth, child, primitive, voxel, and spatial budgets.
- [x] Preserve independently bounded/streamable authoritative voxel/collision/destruction/storage children and decoration handoff through existing adapters.
- [x] Regress determinism/variation, generation-order independence, required/optional behavior, incompatibility, orientation, clearance, support, capacity, recursion/depth, budget failures, graph hashing, cross-region rasterization/provenance, decoration handoff, and conservative voxel-cost boundaries.
- [x] Prove reuse with independent fixtures/consumers and focused coverage for bridge, castle/tower, cliff/platform, and facade/roof families.
- [x] Preserve existing production `CharacterMotor` bridge/gate/cliff traversal and negative-contract regressions without tolerance changes.
- [x] Keep global composition/region/device budgets and shared terrain/storage/renderer APIs unchanged.

## Module-owned focused validation
- [x] Stop using `WorldbuildingGalleryShowcase` as the focused correctness/visual acceptance surface for typed socket composition; keep it only as broad integration/demo coverage.
- [x] Add `Assets/VoxelEngine/Structures/Tests/RuntimeSupport/VoxelEngine.Structures.Tests.RuntimeSupport.asmdef` as a player-compatible assembly referencing production Structures runtime/API only; no NUnit/TestRunner/editor dependency.
- [x] Add `TypedStructuralSocketCompositionSceneDriver` under `Tests/RuntimeSupport`; production code has no dependency back to Tests.
- [x] Route all four demonstrations through the real `StructuralCompositionPlanner.ExpandRoot` production API; do not duplicate socket alignment/compatibility logic in the driver.
- [x] Add deterministic production-result assertions for BridgeSpan, Tower, Platform, and Facade: `Ok`, one child/decision, accepted decision, and exact returned child parent/socket/position/attachment-position/orientation agreement.
- [x] Add required incompatible rejection coverage expecting `StructuralCompositionResult.Incompatible` + `StructuralAttachmentRejectReason.IncompatibleRoleOrTags`.
- [x] Add module-owned `Assets/VoxelEngine/Structures/Tests/Scenes/TypedStructuralSocketComposition.unity` with only bounded validation presentation (four solved lanes, attachment markers, simple ground/camera/light, PASS/FAIL overlay).
- [x] Add `VoxelEngine.Structures.Tests.PlayMode.asmdef` and `TypedStructuralSocketCompositionSceneTests` so the focused semantic gate is independent of broad top-level showcase scenes.
- [x] Confirm the focused assembly/driver/test compile and execute in Unity 6000.5.6f1 via targeted CI.

## CI evidence for focused validation
- [x] Source SHA `79acce98d6594f98b70d4f291a3c9a480f98a72c` contains the focused module validation implementation.
- [x] Run `33351150212` / transport `5d47b909c13948e4106d4e6a00bdeed77d8b6d69`: focused PlayMode class executed 1 test and passed (Unity status 0).
- [x] Diagnose the later failure in `33351150212` before another fix: legacy SceneIssue capture ignored the module scene, built `Assets/Scenes/WorldbuildingGalleryShowcase.unity`, and failed its unrelated 18-view town audit. This is capture-infrastructure coupling, not a semantic socket failure.
- [x] Run `33351379322` / transport `19c1690b722b7685d06b3ca1d1dda8080684d764`: same focused PlayMode test from source SHA `79acce98d6594f98b70d4f291a3c9a480f98a72c`; requested test, capture/no-op, result classification/upload, final status, and workflow all completed green.

## Focused standalone player / visual confirmation
- [ ] Route standalone built-player validation to the exact scene `Assets/VoxelEngine/Structures/Tests/Scenes/TypedStructuralSocketComposition.unity` using the generic module-validation runner once that shared prerequisite is merged.
- [ ] Capture a clean focused overview from the built player and inspect it at full resolution; add close-ups only if they materially improve evidence.
- [ ] Confirm the built focused scene reports PASS and visibly presents all four intended solved root/child compositions without unrelated gallery content.
- [ ] Do not satisfy these tasks by copying the scene under `Assets/Scenes`, re-coupling to `WorldbuildingGalleryShowcase`, weakening acceptance, or modifying shared CI opportunistically.

## Blocker
- [x] Record external prerequisite: current shared `tests-single.yml` / `showcase-player-capture.sh` SceneIssue route only supports legacy `Assets/Scenes/...` replay/capture and forces gallery-specific audit contracts.
- [x] Record that generic module-validation infrastructure was available only on unmerged `fixes/agent-8` when this blocker was identified; agent 5 remains self-contained and does not depend on that branch.
- [x] Re-check current `master` for the generic runner after semantic CI completed: the known module-validation descriptor is still absent, so focused standalone player/capture remains blocked on the shared prerequisite.

## Closure
- [ ] Review final feature diff/cost and verify changes are limited to assignment-required structural production work, regressions/adapters, module-owned focused validation, and this SceneIssue.
- [ ] Complete `issue.json` `resolutionSummary`, `regressionTest`, and `fixCommit` only after final semantic and focused built-player/visual gates are green.
- [ ] Move only this assignment directly `open -> closed`, set `status=fixed` and `resolvedUtc`, after every required acceptance/task above is complete.
- [ ] Fetch latest `origin/master`, merge if advanced, revalidate affected work as needed, push feature branch, then non-force push the exact validated head to `origin/master`; retry if master advances.
