# Verification — Combat/Input production migration

## Exact gate
- Tested source: `216751e491e0bf5e55d6716f91321fca81e2c697` (already merged with then-current `origin/master`).
- Final targeted CI: run `33219897051`, job `99011546783`.
- Focused regression: `VoxelEngine.Tests.PlayMode.CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency`; exactly 1 test executed and passed, Unity exit 0.
- Built application: `Assets/Scenes/CombatPrototype.unity`; build exit 0, 30-second player replay exit 0, 3 real-player screenshots, final verification frame 1600x900.
- Artifact: `single-test-33219897051`, id `9704812978`, SHA-256 `ee341910111039d4b46c6ea4fcc5fe56a1dd2474d6cff569672ea4be714fef7f`.

## Ownership audit
| Classification | Result |
| --- | --- |
| Production authority | `CombatCore`, `ChainCombatBoard`, `ChainExecutionPlan`, `ChainReactionReservationCoordinator`, `ChainRoundReadinessCoordinator`, `ChainEnemyTacticalAI` now live in `Game.Combat.Runtime`; moved source blobs/GUIDs preserve the mature rules. |
| Presentation/demo | Lab controllers, overlays, demo guide/scenario, motion playback, planner UI, plan approval MonoBehaviour, legacy controller, and editor launcher remain under `Assets/CombatPrototype`. |
| Owning-system adapter | `ChainCombatVegetationBridge` remains a lab/composition adapter to `VoxelEngine.Vegetation.Api`; production combat does not take world ownership. |
| Dependency direction | `MountingForce.CombatPrototype` consumes `Game.Combat.Runtime`; production combat does not reference the prototype, UnityEngine, or InputSystem/device APIs. |

## Behavioral parity matrix
The migrated rule files are the same implementations, now compiled by the production combat assembly. Existing behavioral regressions were retargeted directly to that production authority rather than duplicated.

| Contract | Durable regression/evidence |
| --- | --- |
| Movement, occupancy, action budget | `ChainCombatMechanicsV2Tests.NormalActionsActionBudgetAndResetWork` |
| Launch/impulse/collision + reaction reservation/claim | `ChainCombatMechanicsV2Tests.AirborneEventHasCompetingClaimsAndFirstValidClaimOwnsIt`; `CollisionAndTreeEventsBranchBeforeFourPlayerCascadeFinishes` |
| Multi-player planning/readiness | `ChainCombatExecutionPlanV9Tests`; `ChainCombatReadinessV7Tests` |
| Tactical AI intent selection | `ChainCombatEnemyAIV8Tests` |
| Environment interaction through owning API | `ChainCombatProductionVegetationV12Tests.PlayableCascadeMutatesRealProceduralTreeRuntime` |
| Device-neutral combat input | `CombatInputModuleBoundaryTests.SyntheticReader_DrivesCombatMoveThroughDeviceNeutralBoundary` |
| Normal-world production composition | `KentridgeCombatEncounterTests.ForestBandits_ApproachBeginsInPlaceCombatThroughProductionModules` |
| Legacy advanced portal/amplifier mechanics | `ChainCombatPrototypeTests.PortalsPreserveMotionAndForceMultiplierExtendsMotion` |
| Same state + same command stream determinism | final focused `CombatAuthorityMigrationTests.MigratedAuthorityPreservesCascadeAndHasNoPrototypeOrDeviceDependency` executes an identical authored plan on two fresh production boards and compares resulting units, trees, reaction state, cascade metrics, and tactical-AI intents. |

## Blast radius / cost
No combat algorithm was rewritten. Runtime cost and memory shape are unchanged for the migrated rules; the intentional blast radius is assembly ownership/dependency direction, direct test-assembly wiring, and committing the previously editor-generated Combat Lab scene so the canonical built-player gate can exercise it.
