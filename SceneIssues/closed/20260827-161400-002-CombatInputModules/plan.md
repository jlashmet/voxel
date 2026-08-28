# Plan — Combat/Input module boundary analysis

## Evidence / marked regions
- `issue.json` has no captures/circles; the marked scope is architectural: Combat assembly ownership/contracts/runtime, Input device isolation, prototype composition, migration/tests/risks/file moves.
- Current master already contains `Game.Combat.Api/Runtime` and `Game.Input.Api/Runtime` from the later Kentridge production slice. Both APIs and Combat Runtime are engine-free; Combat Runtime references only Combat Api + Input Api.
- `Game.Input.Runtime` owns concrete Unity input. `MountingForce.CombatPrototype` remains separate and its lab controller still owns IMGUI interaction plus direct `ChainCombatBoard` mutation.

## Competing hypotheses
1. Wholesale prototype move — rejected: it would preserve UI/input/rules coupling under new folders.
2. Existing Kentridge modules mean migration is complete — rejected: they cover only lifecycle/grid movement while the richer lab remains independent.
3. Preserve the current API seams and migrate one behavior at a time with parity tests — selected.

## Deliverable / regression
- Refresh `design.md` against the current repository and specify ownership, staged migration, tests, risks, and likely file moves. No production code changes per the captured request.
- Add `CombatInputModuleBoundaryTests.SyntheticReader_DrivesCombatMoveThroughDeviceNeutralBoundary`: synthetic `IPlayerInputReader` → real `CombatInputController` → authoritative `CombatService` mutation, with unrelated enemy state unchanged.

## Blast radius / cost
Docs plus one PlayMode regression only. No production assemblies, scene files, prototype behavior, input polling, or runtime cost changes.

## Verification gate
Run the single regression on the exact final feature SHA through `ci-test/fixes/agent-4`. With no scene/captures, no visual replay is applicable; green behavioral CI is the acceptance evidence for this analysis-only issue.
