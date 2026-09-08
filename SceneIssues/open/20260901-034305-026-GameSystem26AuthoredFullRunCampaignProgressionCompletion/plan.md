# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression **and terminal GameOutcome**, and milestone-driven standalone-player proof. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`. Exact opening identity overlays hierarchy-aware `AuthoredFullRunKentridgeComposition`; continuation coordinates come from recovered `TopDownWorldPhysicalPlan`. The dedicated full-run player and shipped Kentridge integration use the same production composition boundary.

WorldBuilder T26-059 owns `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`: production macro planning/reservations/voxel+water catalogues feed `ShowcaseWorld`, streaming, and `RenderingComposition`. The repaired validation streams 102.4 m and renders 51.2 m, retaining a one-region residency safety margin.

## Current exact-SHA result

Validated System26 production source `ef7e514ce612bb75b2b1e9d8ffeb4f307f3b9530`, request `601401b3ae3c48b8b2dfb6c773c03d3863efe9b2`, run `34195214333`, job `101961328720`, artifact `10044648349`, digest `sha256:fdb1d1ca45b7689e3ad04f82968b9363bc597bef9a3907c2d1e5df96c16c8a55` passed all selected owned tests/players, full-run terminal/frontend proof, WorldBuilder Moordell/Rossdam/water/final PASS, and normal Kentridge integration. Post-r26 commits are evidence/bookkeeping only. Built-player screenshots are prototype/blockout quality; System26 claims semantic/runtime acceptance, not final-art acceptance.

## Remaining gate

T26-043 first depends on System25's production multiplayer infrastructure. `origin/master` still keeps System25 open and does not contain its separate-process harness or production Kentridge multiplayer runtime. Failed exact source `1bd01c2effe23b3f6c9b3e889486d5e7b7a45b98`, request `89cf8233aa0739ce25a0904eb2756d2417475e32`, run `34212118646` isolated a combat-turn/survivability failure: the player was defeated before an authenticated host combat action and generic AI must not consume player turns. Latest observed System25 feature head `55fe435637cbee35454b7c839ff17c500fac2633` tracks the corrected forest-combat player-turn proof. Its exact request `e4b4010a1a2e93770e3a1bf0e271ea6fbe34599c`, run `34218169823`, is **in progress** and must remain untouched while queued/running. T25-010E and the final topology/gameplay/reconnect/rehost evidence remain unchecked until matching proof exists.

Agent-8 must not modify System25, duplicate its process harness, or create client-side campaign authority. System25 completion remains only an infrastructure prerequisite, **not sufficient T26-043 evidence**: after it is validated, closed, and merged to master, merge that master into `fixes/agent-8` and add the narrow System26 multiplayer acceptance consumer using the shared process harness plus production Application/Sessions/UTP/campaign/System15 path. Exact-SHA proof must show authority and separate clients converge on shared Progression and the same immutable terminal GameOutcome.

Only then check T26-043/T26-053, complete T26-054 metadata and `open -> closed`, merge current master again if needed, push `fixes/agent-8`, open/update the PR, enable auto-merge, and monitor the required `affected` gate until merged.
