# Plan — Complete Combat/Input production migration

## Evidence / discriminator
- Architecture issue: `captures` is empty, so there are 0 marked regions/poses; validation is behavioral plus the real built-player Combat Lab.
- Kentridge already composed the narrow `Game.Combat` slice; the missing authority was six engine-independent rule owners still compiled under `Assets/CombatPrototype`.
- “Existing Game runtime is sufficient” was falsified by those rule/state owners. “Move the whole prototype” was falsified by Unity presentation/debug/adapters. Selected: move only reusable deterministic authority intact into `Game.Combat.Runtime` and make the lab consume it.

## Class audit / fix
- Production authority: `CombatCore`, `ChainCombatBoard`, `ChainExecutionPlan`, `ChainReactionReservationCoordinator`, `ChainRoundReadinessCoordinator`, `ChainEnemyTacticalAI`; moved with unchanged implementations/GUIDs.
- Presentation/demo stays in the lab: controllers, overlays, guide/scenario, playback, planner UI, plan approval MonoBehaviour, legacy controller, editor launcher.
- `ChainCombatVegetationBridge` remains an owning-system adapter through `VoxelEngine.Vegetation.Api`.
- Dependency is one-way: prototype -> `Game.Combat.Runtime`; production combat has no prototype, UnityEngine, or InputSystem/device dependency.

## Runtime evidence
- Early resolver failures exposed blank replay metadata; assigned issue was bound to committed `Assets/Scenes/CombatPrototype.unity` at 1600x900 without inventing captures.
- Run `33218532934` exposed non-transitive test asmdef references; existing parity tests gained only a direct `Game.Combat.Runtime` test dependency.
- Final exact source `216751e491e0bf5e55d6716f91321fca81e2c697`, run `33219897051`: strengthened focused regression passed (1/1); CombatPrototype player build and 30-second replay exited 0; 3 real frames; final 1600x900; artifact `9704812978`.

## Regression / parity / blast radius
- `CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency` proves ownership/dependency boundaries, reaction reservation, identical-plan deterministic replay from identical state/commands, and deterministic enemy intents.
- Retargeted regressions cover movement/occupancy, force/collision, reaction ownership, multi-player planning/readiness, AI, production vegetation, semantic input isolation, Kentridge in-world composition, and legacy portal/amplifier mechanics; `verification.md` records the matrix and final artifact.
- No combat algorithm was rewritten; runtime CPU/memory behavior is unchanged. Intentional blast radius is assembly ownership, test wiring, and committing the representative lab scene.
