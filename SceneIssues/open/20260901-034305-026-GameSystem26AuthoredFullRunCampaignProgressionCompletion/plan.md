# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and milestone-driven built-player full-run evidence. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` now enters through `KentridgePlayableFullRunBootstrap`. The bootstrap preserves the opening-only Kentridge planner for exact rich pub/hidden-space realization, overlays those opening semantic identities onto hierarchy-aware `AuthoredFullRunKentridgeComposition`, and boots `AuthoredFullRunCampaignContent` against the recovered multi-settlement physical plan. The shipped scene asserts `IsFullRun` and emits `KENTRIDGE_AUTHORED_FULL_RUN_READY`; the normal Kentridge player scenario requires that marker.

`Assets/Game/Composition/Kentridge/Playable/Validation/KentridgeFullRunCampaignValidation.unity` is the owning module-local full-run player proof. It uses the shipped bootstrap, `GameSessionOrchestrator`, `ApplicationFlowCoordinator`, public Campaign facts, and production `EncounterRegistry`. It advances opening -> Awon -> Medrare -> church/Angel -> Rorik -> Moordell -> Rossdam -> mayor -> Logan -> lower castle, then asserts System15 success at revision 1, `ApplicationScreen.Outcome`, and normal return-to-frontend teardown. Its paired scenario requires bounded semantic milestone logs.

## Current validation state

Historical exact runs already prove Story/campaign ownership and hierarchy/site realization. Request `dcc9f36e64a4edaebe250560a89c2754aece2931` / run `34158903057` completed with a compile failure exposing a missing direct Kentridge API import; fixed at `6da358b28351b3b1c8645e65bdf862ba66cdcafb`. Request `715da043608eb80f167c8d404385b7bc6ba8b224` / run `34161473335` then exposed two narrower compile defects: missing `Game.WorldBuilder.Runtime` in the composition regression and an ambiguous validation `KentridgeDefinition`; both are fixed on the feature branch.

Hypothesis A: those namespace/type-resolution defects were the remaining compile blockers. Hypothesis B: after compilation, the real full-run player proof may expose a semantic/runtime dead end. Next discriminating experiment is one new direct-child exact-SHA request from the stabilized feature head; do not speculate further before that result.

## External prerequisite / closure

T26-043 remains independently owned by System25. `fixes/agent-7` still records separate-process authority/client topology, shared progression, reconnect/leave and release acceptance as incomplete; System26 must not duplicate that harness or authority.

After the next exact run: fix only demonstrated System26 failures. When automatic module tests, the full-run module-local player, and production Kentridge integration are green, record evidence and check T26-021/022/044/045/046/053/058. Recheck System25 and check T26-043 only from its authoritative evidence. Only when every task is complete: populate closure fields, move open -> closed, merge current `origin/master`, then promote through a fresh PR + auto-merge and required `affected` gate.

Preserve existing performance/memory budgets and validation architecture; no readiness forcing, radius/budget widening, or alternate runtime paths.