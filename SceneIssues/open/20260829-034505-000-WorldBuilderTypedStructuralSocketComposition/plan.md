# Plan

## Acceptance / ownership
- Canonical production remains `FeatureDefinition` + typed `SlotSpec` + `ShapeOp.CallSlot` + `FeatureCatalogue` + descendant-aware `FeatureRegionBuild`; there is no second structural solver.
- Shared structural APIs stay semantic/config-driven and scene/material-ID agnostic. Scene-specific presentation and evidence policy stay outside production Structures code.
- Mechanical acceptance remains the already-established deterministic typed attachment/budget, authoritative independently bounded child voxelization, reuse, traversal, negative-contract, and bounded-cost coverage.
- Visual acceptance for this feature is now module-owned. `WorldbuildingGalleryShowcase` remains a broad integration/demo surface, but it is no longer the focused acceptance scene for typed socket composition.

## Focused module-validation direction (2026-08-30)
- Own the validation surface under `Assets/VoxelEngine/Structures/Tests`:
  - `Scenes/TypedStructuralSocketComposition.unity`
  - `RuntimeSupport/VoxelEngine.Structures.Tests.RuntimeSupport.asmdef`
  - `RuntimeSupport/TypedStructuralSocketCompositionSceneDriver.cs`
  - `PlayMode/VoxelEngine.Structures.Tests.PlayMode.asmdef`
  - `PlayMode/TypedStructuralSocketCompositionSceneTests.cs`
- `RuntimeSupport` is player-compatible and references only production `Structures.Api` / `Structures.Runtime` plus their runtime dependencies. It has no NUnit, TestRunner, or editor dependency. Production assemblies do not depend on anything under `Tests`.
- The scene driver contains no independent socket compatibility/alignment algorithm. It builds deterministic typed catalogues, calls `StructuralCompositionPlanner.ExpandRoot`, stages the returned production instances/attachment positions, and reports deterministic PASS/FAIL.
- Four independent demonstrations exercise `BridgeSpan`, `Tower`, `Platform`, and `Facade`. For each, acceptance requires `Ok`, exactly one accepted attachment, and exact agreement between the production attachment decision and returned child parent/socket/position/attachment-position/orientation.
- A required incompatible socket is separately required to return `StructuralCompositionResult.Incompatible` and `StructuralAttachmentRejectReason.IncompatibleRoleOrTags`.
- Semantic validation is the correctness gate. A clean screenshot of the focused scene is human-readable confirmation and must not become a replacement for semantic assertions.

## Evidence
- Source SHA `79acce98d6594f98b70d4f291a3c9a480f98a72c` contains the focused scene, player-safe runtime-support assembly/driver, module-owned PlayMode assembly, and focused PlayMode test.
- Targeted CI run `33351150212` on transport commit `5d47b909c13948e4106d4e6a00bdeed77d8b6d69` compiled successfully and executed the focused PlayMode class: 1 test case, Unity exit status 0. The workflow later failed only in legacy real-player capture because a SceneIssue request hardwired `Assets/Scenes/WorldbuildingGalleryShowcase.unity` and its unrelated 18-view town audit.
- That failure is a discriminator, not a socket-composition regression: the player itself exited 0 with zero harness assertion failures, while the legacy gallery capture contract reported the missing town-architecture evidence.
- Targeted semantic-only run `33351379322` on transport commit `19c1690b722b7685d06b3ca1d1dda8080684d764` is fully green. It ran the same focused PlayMode class from source SHA `79acce98d6594f98b70d4f291a3c9a480f98a72c`; `Run requested test`, capture/no-op, result classification/upload, and final commit status all completed successfully.

## Capture integration blocker
- The current shared `tests-single.yml` / `showcase-player-capture.sh` path derives SceneIssue replay scenes from `Assets/Scenes/...`; it cannot yet target `Assets/VoxelEngine/Structures/Tests/Scenes/TypedStructuralSocketComposition.unity` as a standalone module validation scene.
- Agent 8 has a generic module-validation architecture on `fixes/agent-8`, but it was not merged at the time this blocker was recorded. Agent 5 must not depend on that unmerged branch or modify shared workflow infrastructure opportunistically.
- Therefore do not move this validation back into `WorldbuildingGalleryShowcase`, do not duplicate the focused scene under `Assets/Scenes`, and do not weaken acceptance. Keep the focused scene ready for the generic runner once that prerequisite lands.

## Cost / blast radius
- No global composition/region/device budget, terrain policy, storage/residency policy, renderer API, or `CharacterMotor` tolerance changes are required for the focused validation pivot.
- Production solver/runtime code is unchanged by the focused validation scene work.
- New dependencies point only from test/runtime-support assemblies toward production; never production toward tests.
- The scene visualization is bounded to four small solved root/child examples plus markers/ground/camera/light and exists only under `Structures/Tests`.

## Next non-blocked work
1. Re-check whether the generic module-validation runner has merged before declaring the visual prerequisite blocked.
2. Keep all existing production structural regressions green; fix only demonstrated failures.
3. When the generic module-validation runner is merged, route standalone player build/capture to the exact focused scene path and inspect the resulting overview at full resolution.
4. Only after the focused semantic gate and focused standalone player/capture are both green, perform final assignment-only diff/cost review, complete issue metadata, close the SceneIssue, merge current master as required, and push the exact validated head.
