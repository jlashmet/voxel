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
- Runs `33218244708`/`33218424949` never reached Unity because reopened replay metadata was blank; bind the assigned issue to committed `Assets/Scenes/CombatPrototype.unity` at 1600×900 without inventing captures.
- Run `33218532934` then exposed non-transitive asmdef compilation in the unchanged prototype parity tests; add their direct `Game.Combat.Runtime` test dependency only.
- Run `33218670471` passed the focused authority/cascade regression and the real built-player Combat Lab: build/player exit 0, two real frames, final 1600×900.

## Regression / parity / blast radius
- Focused `CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency` proves production assembly ownership, no prototype/Unity/InputSystem dependency, uppercut → reaction reservation/claim, identical plan replay from identical state/commands, and deterministic enemy intent selection.
- Existing retargeted parity tests cover movement/occupancy, damage/force/knockback, reaction ownership, multi-player execution/readiness, and production vegetation handoff. `CombatInputModuleBoundaryTests` covers semantic input isolation; `KentridgeCombatEncounterTests` covers in-world production composition and persistent actors.
- Combat algorithms are unchanged; moved blobs/GUIDs are preserved. CPU/memory behavior is unchanged; blast radius is assembly ownership, test dependency wiring, and committing/binding the previously editor-only lab scene.

## Remaining gates
Run the strengthened exact-SHA focused regression + built-player replay, store durable verification evidence, then pending/closed bookkeeping, merge current master, and non-force publish exact feature head.
