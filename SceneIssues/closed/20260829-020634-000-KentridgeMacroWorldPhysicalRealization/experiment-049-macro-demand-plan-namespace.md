# Experiment 049 — macro demand physical-plan namespace

## Trigger
Exact requests `bd10ce4f594f1619e8bf48a866459c6ac0aa174f` (run `33937792014`) and `59ed8938f6ace32c89817f0c417f45fddfabb26e` (run `33938299650`) both stopped before requested-test or player runtime execution with the same sole compiler symptom:

`KentridgeMacroWorldContentDemandDriver.cs(...): CS0246 TopDownWorldPhysicalPlan could not be found`.

The second source had already added `using Game.WorldBuilder.Api;`, so a further speculative namespace/dependency change was not permitted without isolating the declaration and owning assembly.

## Isolation
`TopDownWorldPhysicalPlan`, `TopDownWorldSettlementPlan`, and `TopDownWorldPhysicalPlanner` are declared by `Assets/Game/WorldBuilder/Generation/Voxel/TopDownWorldPhysicalPlanner.cs` in namespace `MountingForce.WorldGen.Voxel`.

`Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Game.Kentridge.PlayableSlice.asmdef` already references assembly `MountingForce.WorldGen.Voxel`. Therefore the repeated compiler failure is not a missing assembly dependency: the new helper simply omitted `using MountingForce.WorldGen.Voxel;`.

## Correction
Source `52cb65457f1b8c7dd637895700a2856e0bd51511` adds only the owning namespace import. No streaming radius, generation budget, renderer/device budget, residency policy, force-generation path, or evidence acceptance criterion changes.

## Next proof
Run the existing focused GPU-liveness regression through `ci-test/fixes/agent-6` on the exact corrected feature source, retain repository-derived module validation, and retain the 180-second SceneIssue replay. A new request is justified because both prior requests completed product-red before runtime and the root cause is now isolated.
