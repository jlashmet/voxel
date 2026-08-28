# Plan — Complete Combat/Input production migration

## Evidence / discriminator
- Architecture issue: `captures` is empty, so there are 0 marked regions/poses; validation is behavioral + built-player `CombatPrototype` scene.
- Kentridge already uses `Game.Combat` through `KentridgeForestBanditEncounter`; the missing scope is the richer lab authority.
- Audit found six engine-independent authority files under `Assets/CombatPrototype`: `CombatCore`, `ChainCombatBoard`, `ChainExecutionPlan`, reaction reservation, round readiness, and enemy tactical AI. They import only `System*` and each other, yet currently compile into `MountingForce.CombatPrototype`.
- Hypothesis “the existing Game runtime is sufficient” is falsified by those board/rule/state owners. Hypothesis “move the whole prototype” is falsified by the remaining Unity presentation, debug, and environment-adapter code. Selected: move only reusable deterministic authority intact into `Game.Combat.Runtime` and make the lab consume it.

## Class audit / selected fix
**Production authority → `Game.Combat.Runtime`:** `CombatCore.cs`, `ChainCombatBoard.cs`, `ChainExecutionPlan.cs`, `ChainReactionReservationCoordinator.cs`, `ChainRoundReadinessCoordinator.cs`, `ChainEnemyTacticalAI.cs` (with existing Unity GUIDs preserved).

**Demo/presentation only:** activation/event/enemy-intent overlays, demo guide, scripted `ChainCombatDemoScenario`, `ChainCombatLabController`, motion playback, setup panel, `ChainExecutionPlanner` UI, `ChainPlanApprovalCoordinator` MonoBehaviour, legacy `CombatPrototypeController`, editor launcher.

**Owning-system adapter:** `ChainCombatVegetationBridge` remains in the lab and translates semantic tree impacts/fells through `VoxelEngine.Vegetation.Api`; combat does not acquire a vegetation/world dependency.

Retarget `MountingForce.CombatPrototype.asmdef` one-way to `Game.Combat.Runtime`. No production combat assembly may reference the prototype, UnityEngine, or InputSystem/device APIs.

## Regression / blast radius / cost
- Add `CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency`: asserts assembly ownership/dependency direction and runs the real migrated uppercut → airborne reaction → P2 reservation/claim plus deterministic enemy-intent planning.
- Existing `KentridgeCombatEncounterTests` covers production Kentridge composition through `Game.Combat`.
- No combat algorithm is rewritten; source blobs and script GUIDs move intact. Runtime CPU/memory cost is unchanged; blast radius is assembly ownership/compile dependency only.

## Remaining gates
Push source/test commit, run one exact-SHA targeted CI request + built-player Combat Lab replay, then pending/closed bookkeeping, refresh/merge current master, and non-force publish exact feature head.
