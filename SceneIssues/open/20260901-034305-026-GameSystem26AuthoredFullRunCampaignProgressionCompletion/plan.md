# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`: exact rich opening geometry is overlaid by semantic identity onto hierarchy-aware `AuthoredFullRunKentridgeComposition`, while continuation coordinates come only from the recovered `TopDownWorldPhysicalPlan`. The shipped scene requires `IsFullRun` and logs `KENTRIDGE_AUTHORED_FULL_RUN_READY`.

`Assets/Game/Composition/Kentridge/Playable/Validation/KentridgeFullRunCampaignValidation.unity` drives the shipped bootstrap through `GameSessionOrchestrator` and `ApplicationFlowCoordinator`, advances the canonical route through real Encounter facts, and asserts System15 success revision 1, Outcome screen, and normal frontend return.

WorldBuilder is also an affected player-visible module. Its prior only owned player scene (`Validation/SecretDiscovery`) exercises cave-secret generation, not the new top-down terrain/roads/towns/water path. T26-059 therefore adds `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`, which uses the production macro planner, reservation adapter, voxel/water catalogues, `ShowcaseWorld`, streaming, and rendering composition with semantic Moordell/Rossdam/water survey targets.

## Current validation state

Exact request `3796acf10f04fe266413f0956ebbd59e954336ad`, source `d6dd7a8100841b997b064afefc4d1e0fca19e333`, run `34164164923`, job `101871803067` reached automatic module validation but Unity aborted before tests. Artifact `10034721892` (`sha256:4e000d7a63b0235ca006f89c037ccc89369262813133ed5a9274be505d1cf6a9`) reports one compiler defect: `AuthoredFullRunKentridgeCompositionTests` accessed non-public `AuthoredTownPlan.BackendPlan`. The regression now uses the public authored-town plan for campaign planning and deterministic Kentridge settlement facts.

Next discriminator: exact-SHA automatic module/player validation from the new feature head, including the new WorldBuilder-owned scene. Fix only demonstrated failures.

## External prerequisite / closure

T26-043 remains owned by System25. Its authoritative branch still has separate-process authority/client topology, shared progression, reconnect/rehost and final evidence incomplete; System26 must not duplicate that harness or authority.

After System26 exact validation is green, record evidence/check independent tasks. Check T26-043 only from authoritative System25 evidence. Close only when every checkbox is complete; then merge current master, promote by PR + auto-merge, and require the `affected` gate.
