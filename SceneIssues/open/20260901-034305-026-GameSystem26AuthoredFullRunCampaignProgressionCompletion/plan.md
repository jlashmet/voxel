# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression **and terminal GameOutcome**, and milestone-driven standalone-player proof. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`. Exact opening identity overlays hierarchy-aware `AuthoredFullRunKentridgeComposition`; continuation coordinates come from recovered `TopDownWorldPhysicalPlan`. The dedicated full-run player and shipped Kentridge integration use the same production composition boundary.

WorldBuilder T26-059 owns `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`: production macro planning/reservations/voxel+water catalogues feed `ShowcaseWorld`, streaming, and `RenderingComposition`. The repaired validation streams 102.4 m and renders 51.2 m, retaining a one-region residency safety margin.

## Current exact-SHA result

Validated System26 production source `ef7e514ce612bb75b2b1e9d8ffeb4f307f3b9530`, request `601401b3ae3c48b8b2dfb6c773c03d3863efe9b2`, run `34195214333`, job `101961328720`, artifact `10044648349`, digest `sha256:fdb1d1ca45b7689e3ad04f82968b9363bc597bef9a3907c2d1e5df96c16c8a55` passed all selected owned tests/players, full-run terminal/frontend proof, WorldBuilder Moordell/Rossdam/water/final PASS, and normal Kentridge integration. Post-r26 commits are evidence/bookkeeping only. Built-player screenshots are prototype/blockout quality; System26 claims semantic/runtime acceptance, not final-art acceptance.

## Remaining gate

T26-043 depends on System25's production multiplayer infrastructure, which is still absent from `origin/master`. System25 exact source `55fe435637cbee35454b7c839ff17c500fac2633`, request `e4b4010a1a2e93770e3a1bf0e271ea6fbe34599c`, run `34218169823`, job `102034779084`, artifact `10053235307`, digest `sha256:779a04afa7fbde066ef9ac2728c1a04cbd64f3c4e946ad377e3fff026037f2a5` completed **failure** in automatic module validation. The corrected combat path now passes topology, baseline, contention, shared progression, Continuity interruption, and authority/client-B combat-vitality convergence. Failure moved to reconnect: relaunched client A reaches `party-joined`, then Application fails `SessionPrepareFailed: Replicated gameplay state is not ready for client composition`, so `topology-ready` times out. System25 must fix and exact-SHA prove that prerequisite; Agent-8 must not modify it or duplicate its harness.

After System25 is validated, closed, and merged to master, merge that master into `fixes/agent-8` and add the narrow System26 multiplayer consumer using shared process infrastructure plus production Application/Sessions/UTP/campaign/System15. Exact-SHA proof must show authority and separate clients converge on shared Progression and the same immutable terminal GameOutcome. Then complete T26-043/T26-053/T26-054, move `open -> closed`, reconcile current master, and promote via PR + auto-merge `affected` gate.
