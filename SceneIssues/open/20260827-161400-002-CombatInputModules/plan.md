# Plan — Complete Combat/Input production migration

## Evidence / discriminator
- Architecture issue: `captures` is empty, so there are 0 marked regions/poses; validation is behavioral plus built-player `CombatPrototype` replay.
- Kentridge already uses `Game.Combat` through `KentridgeForestBanditEncounter`; the missing scope is richer lab authority.
- Audit found six engine-independent authority files under `Assets/CombatPrototype`: `CombatCore`, `ChainCombatBoard`, `ChainExecutionPlan`, reaction reservation, round readiness, and enemy tactical AI. They import only `System*`/each other yet compiled into the prototype assembly.
- “Existing Game runtime is sufficient” is falsified by those board/rule/state owners. “Move the whole prototype” is falsified by remaining Unity presentation/debug/adapters. Selected: move only reusable deterministic authority intact into `Game.Combat.Runtime` and make the lab consume it.

## Class audit / selected fix
**Production authority → `Game.Combat.Runtime`:** `CombatCore.cs`, `ChainCombatBoard.cs`, `ChainExecutionPlan.cs`, `ChainReactionReservationCoordinator.cs`, `ChainRoundReadinessCoordinator.cs`, `ChainEnemyTacticalAI.cs`, preserving source content and Unity GUIDs.

**Demo/presentation only:** activation/event/enemy-intent overlays, demo guide/scenario, lab controller, motion playback, setup panel, planner UI, plan-approval MonoBehaviour, legacy controller, editor launcher.

**Owning-system adapter:** `ChainCombatVegetationBridge` remains in the lab and translates semantic tree events through `VoxelEngine.Vegetation.Api`; combat does not acquire world ownership.

`MountingForce.CombatPrototype` now references `Game.Combat.Runtime` one-way. Production combat may not reference prototype, UnityEngine, or InputSystem/device APIs.

## Runtime evidence / gate correction
- CI run `33218244708` reached no Unity code: request resolution failed because the assigned `scenePath` `Assets/Scenes/CombatPrototype.unity` did not exist. The prototype README confirms the lab was editor-generated and previously required no committed scene, making built-application validation impossible.
- Add that exact committed scene with `ChainCombatLabController`; at runtime it constructs the migrated production board/coordinators and the existing playable lab presentation. This fixes the validation surface without duplicating rules.

## Regression / blast radius / cost
- `CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency` asserts assembly/dependency ownership and executes uppercut → airborne → P2 reservation/claim plus deterministic enemy planning.
- Existing `KentridgeCombatEncounterTests` covers production Kentridge composition through Game modules.
- Combat algorithms are unchanged; moves preserve blobs/GUIDs. CPU/memory behavior is unchanged; blast radius is assembly ownership plus committing the previously editor-only lab scene.

## Remaining gates
Push scene/source SHA, run final exact-SHA focused CI + built-player replay, then pending/closed bookkeeping, merge current master, and non-force publish exact feature head.
