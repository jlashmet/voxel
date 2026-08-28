# Plan — Complete Combat/Input production migration

## Evidence / discriminator
- Architecture issue: `captures` is empty, so there are 0 marked regions/poses; validation is behavioral plus a built-player Combat Lab replay.
- Kentridge already composes `Game.Combat`; the missing scope is richer lab authority.
- Audit found six engine-independent authority files under `Assets/CombatPrototype`: `CombatCore`, `ChainCombatBoard`, `ChainExecutionPlan`, reaction reservation, round readiness, and enemy tactical AI. They import only `System*`/each other yet compiled into the prototype assembly.
- “Existing Game runtime is sufficient” is falsified by those board/rule/state owners. “Move the whole prototype” is falsified by Unity presentation/debug/adapters. Selected: move only reusable deterministic authority intact into `Game.Combat.Runtime` and make the lab consume it.

## Class audit / selected fix
**Production authority:** the six files above move to `Game.Combat.Runtime`, preserving source content and Unity GUIDs.

**Demo/presentation only:** overlays, guide/scenario, lab controller, motion playback, setup panel, planner UI, plan-approval MonoBehaviour, legacy controller, editor launcher.

**Owning-system adapter:** `ChainCombatVegetationBridge` stays in the lab and translates tree events through `VoxelEngine.Vegetation.Api`; combat does not acquire world ownership.

`MountingForce.CombatPrototype` references `Game.Combat.Runtime` one-way. Production combat may not reference prototype, UnityEngine, or InputSystem/device APIs.

## Runtime evidence
- Runs `33218244708`/`33218424949` reached no Unity code because the reopened issue had blank replay metadata. The exact request tree did contain the new lab scene. Bound the assigned issue to `Assets/Scenes/CombatPrototype.unity` at 1600×900 without inventing captures.
- Run `33218532934` passed request resolution and reached Unity. Compilation failed because existing `MountingForce.CombatPrototype.Tests.PlayMode` referenced only the lab assembly; Unity asmdef references are non-transitive, so legacy parity tests could not see moved types. Player build failed on the same compiler errors.
- Add a direct `Game.Combat.Runtime` reference to that existing test assembly. No test logic or production algorithm changes.

## Regression / blast radius / cost
- `CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency` asserts assembly/dependency ownership and executes uppercut → airborne → P2 reservation/claim plus deterministic enemy planning.
- Existing CombatPrototype parity suite remains compiled against the same types; `KentridgeCombatEncounterTests` covers production Kentridge composition.
- Combat algorithms are unchanged; moves preserve blobs/GUIDs. CPU/memory behavior is unchanged; blast radius is assembly ownership, test dependency wiring, and committing/binding the previously editor-only lab scene.

## Remaining gates
Push corrected source SHA, run exact-SHA focused CI + built-player replay, then pending/closed bookkeeping, merge current master, and non-force publish exact feature head.
