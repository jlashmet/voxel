# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`: exact rich opening geometry is overlaid by semantic identity onto hierarchy-aware `AuthoredFullRunKentridgeComposition`, while continuation coordinates come only from the recovered `TopDownWorldPhysicalPlan`. The dedicated Kentridge validation drives the canonical route through production session/application seams to System15 success and frontend aftermath.

WorldBuilder is also affected player-visible runtime. T26-059 owns `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`: production macro planning/reservations/voxel+water catalogues feed `ShowcaseWorld`, production streaming and `RenderingComposition`, surveying Moordell, Rossdam and authored water. Readiness uses only existing production signals: the target region must be `ShowcaseWorld.IsGenerated(...)` and `RenderingComposition.HasCompletePublishedNearSurfaceCoverage()` for four stable frames.

## Validation state / hypotheses

Request `3796acf10f04fe266413f0956ebbd59e954336ad`, source `d6dd7a8100841b997b064afefc4d1e0fca19e333`, run `34164164923`, artifact `10034721892` (`sha256:364595f48f11e3c93445408110654e41b1593111a2a7b4498fffb3f7af3afc12`) exposed stale `AuthoredTownPlan.BackendPlan` test usage; fixed.

Request `7d5ee6f751e0d4d72d3061b9a4474703921a5d58`, source `a3a7f9203f925d42a8bafc617e656b1995e003bd`, run `34170036879`, job `101888444958`, artifact `10037000615` (`sha256:03cc22112a8862b93f48c8fc04fa8d8b43d8b41fcc8cddb2dc5c586eb9579ad6`) proved automatic discovery of T26-059 but compilation stopped on two validation-boundary defects: nonexistent `IsPresentationColumnContentSettled` and missing direct `VoxelEngine.Storage.Api`. Both are corrected without changing production semantics.

Hypothesis A: those two T26-059 boundary defects were the remaining compile blockers. Hypothesis B: once compilation succeeds, a real Kentridge/WorldBuilder player target may expose a semantic, streaming or visual defect. Next discriminator is one new direct-child exact-SHA automatic run; fix only demonstrated failures.

## External prerequisite / closure

T26-043 remains owned by System25. Check it only from authoritative separate-process multiplayer progression/outcome evidence; do not duplicate its harness. Close only after every task is complete, then merge current master into the feature and promote by fresh PR + auto-merge with required `affected` gate.
