# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression **and terminal GameOutcome**, and milestone-driven standalone-player proof. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`. Exact opening identity overlays hierarchy-aware `AuthoredFullRunKentridgeComposition`; continuation coordinates come from recovered `TopDownWorldPhysicalPlan`. The dedicated full-run player and shipped Kentridge integration use the same production composition boundary.

WorldBuilder T26-059 owns `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`: production macro planning/reservations/voxel+water catalogues feed `ShowcaseWorld`, streaming, and `RenderingComposition`. The repaired validation streams 102.4 m and renders 51.2 m, keeping one 51.2 m region as residency safety margin while retaining generated-region plus published-near-surface coverage readiness.

## Current exact-SHA result

Validated production source: `ef7e514ce612bb75b2b1e9d8ffeb4f307f3b9530` under exact request `601401b3ae3c48b8b2dfb6c773c03d3863efe9b2`, run `34195214333`, job `101961328720`, artifact `10044648349` (`sha256:fdb1d1ca45b7689e3ad04f82968b9363bc597bef9a3907c2d1e5df96c16c8a55`). The automatic gate passed all selected owned tests/players. Full-run reached all campaign milestones, exactly-one success, frontend aftermath and final PASS. WorldBuilder reached Moordell, Rossdam and water `PASS coverage=True` plus final validation PASS. Normal `KentridgePlayableSlice` emitted `KENTRIDGE_AUTHORED_FULL_RUN_READY` and passed. Post-r26 commits are evidence/bookkeeping only.

Direct screenshot review classifies normal Kentridge presentation as prototype/blockout quality. System26 claims semantic/runtime acceptance only; it does not claim production-quality art acceptance. See `ci-evidence-601401b3.md`.

## Remaining gate

T26-043 first depends on System25's production multiplayer infrastructure. Current observed System25 source is `1bd01c2effe23b3f6c9b3e889486d5e7b7a45b98`; exact request `89cf8233aa0739ce25a0904eb2756d2417475e32`, run `34212118646`, job `102015303375` is queued and must remain untouched while queued/running. That source adds the T25-010E production correction for continuing to advance the Running session graph after Application reaches InGame; System25's binding topology/gameplay/reconnect/rehost checklist remains incomplete.

System25 completion is an infrastructure prerequisite, **not sufficient T26-043 evidence by itself**. Its topology scenario asserts shared `progression-converged` but no terminal outcome, and the validation composes `RunningOutcomeQuery.Instance`, whose snapshot is always `GameOutcomeSnapshot.Running()`. After System25 is validated, closed, and merged to master, merge that master into `fixes/agent-8` and add the narrow System26 multiplayer acceptance consumer using the shared process harness plus production Application/Sessions/UTP/campaign/System15 path. Exact-SHA proof must show authority and separate clients converge on shared Progression and the same immutable terminal GameOutcome; do not duplicate networking or campaign authority.

Only then check T26-043/T26-053, complete T26-054 metadata and `open -> closed`, merge current master again if needed, push `fixes/agent-8`, open/update the PR, enable auto-merge, and monitor the required `affected` gate until merged.
