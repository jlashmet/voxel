# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`: exact rich opening geometry is overlaid by semantic identity onto hierarchy-aware `AuthoredFullRunKentridgeComposition`, while continuation coordinates come only from the recovered `TopDownWorldPhysicalPlan`. Both the shipped player and dedicated full-run validation must obtain Kentridge through `Game.Kentridge.PlayableSlice.KentridgeDefinition`, which enters `WorldBuilderTownAuthoring` and preserves the exact authored town/settlement pair required by `KentridgePlayableWorldBuilderBridge`.

WorldBuilder is also affected player-visible runtime. T26-059 owns `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`: production macro planning/reservations/voxel+water catalogues feed `ShowcaseWorld`, production streaming and `RenderingComposition`, surveying Moordell, Rossdam and authored water. Readiness uses only existing production signals: the target region must be `ShowcaseWorld.IsGenerated(...)` and `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()` for four stable frames.

## Validation state / hypotheses

Request `3796acf10f04fe266413f0956ebbd59e954336ad` exposed stale `AuthoredTownPlan.BackendPlan` test usage; fixed. Request `7d5ee6f751e0d4d72d3061b9a4474703921a5d58` exposed two T26-059 compile-boundary defects; both were fixed without changing production semantics.

Request `cdba76adecef1ea1104cbd8c7b8a3b1f2ea4da08`, source `360cad203f30f7b771ff2c93798cfa29d4a7591c`, run `34178608752`, job `101912883262`, artifact `10040106171` (`sha256:b5291fbbdffce3eca44940d19ca76f29097f9e1ead25441a761014666656ccd1`) compiled and passed every selected persistent test assembly plus CaveWorldBuilder and Kentridge encounter players. The full-run player then failed before `physical-world-ready`: validation had called legacy `MountingForce...KentridgeDefinition.Build`, so `KentridgePlayableWorldBuilderBridge.Resolve` correctly rejected a settlement not authored through the scene-session WorldBuilder seam. Commit `c36dca64a08aa2af50ee16b97fba5a9e640d2a48` routes validation settlement/id selection through the shipped compatibility wrapper.

Next discriminator: one fresh direct-child exact-SHA automatic run. If it passes this seam, inspect the subsequent Kentridge full-run, T26-059 WorldBuilder, and normal Kentridge integration player evidence; fix only demonstrated failures.

## External prerequisite / closure

T26-043 remains owned by System25. Check it only from authoritative separate-process multiplayer progression/outcome evidence; do not duplicate its harness. Close only after every task is complete, then merge current master into the feature and promote by fresh PR + auto-merge with required `affected` gate.
