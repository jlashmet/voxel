# 26 Authored full-run campaign progression & completion — implementation plan

## Acceptance / ownership

Complete the authored Kentridge -> Rorik/Moordell/Rossdam/Logan route, exactly-once System15 terminal/frontend aftermath, mid-run restore, shared multiplayer progression, and milestone-driven standalone-player proof. Story consumes semantic facts; System11 owns objectives, System15 outcomes, Systems16/14 persistence/restore. No fake regions, parallel chapter authority, alternate transport, or privileged progression shortcuts.

## Selected production path

`KentridgePlayableSlice` enters through `KentridgePlayableFullRunBootstrap`. Exact opening identity overlays hierarchy-aware `AuthoredFullRunKentridgeComposition`; continuation coordinates come from recovered `TopDownWorldPhysicalPlan`. The dedicated full-run player and shipped Kentridge integration use the same production composition boundary.

WorldBuilder T26-059 owns `Assets/Game/WorldBuilder/Validation/TopDownPhysicalWorld/`: production macro planning/reservations/voxel+water catalogues feed `ShowcaseWorld`, streaming, and `RenderingComposition`. The repaired validation streams 102.4 m and renders 51.2 m, keeping one 51.2 m region as residency safety margin while retaining generated-region plus published-near-surface coverage readiness.

## Current exact-SHA result

Validated production source: `ef7e514ce612bb75b2b1e9d8ffeb4f307f3b9530` under exact request `601401b3ae3c48b8b2dfb6c773c03d3863efe9b2`, run `34195214333`, job `101961328720`, artifact `10044648349` (`sha256:fdb1d1ca45b7689e3ad04f82968b9363bc597bef9a3907c2d1e5df96c16c8a55`). The automatic gate passed all selected owned tests/players. Full-run reached all campaign milestones, exactly-one success, frontend aftermath and final PASS. WorldBuilder reached Moordell, Rossdam and water `PASS coverage=True` plus final validation PASS. Normal `KentridgePlayableSlice` emitted `KENTRIDGE_AUTHORED_FULL_RUN_READY` and passed.

Direct screenshot review classifies normal Kentridge presentation as prototype/blockout quality. System26 claims semantic/runtime acceptance only; it does not claim production-quality art acceptance. See `ci-evidence-601401b3.md`.

Current branch head contains only post-r26 evidence/checklist bookkeeping beyond the validated production source; no production implementation changed after r26.

## Remaining gate

T26-043 is externally blocked by System25. At source `6401622dab168f474aa2abc19eb8828e36cbaa07`, its authoritative checklist still leaves real authority/client topology, baseline convergence, contention/conservation, combat/vitality, shared progression, reconnect/current-state recovery, explicit leave, rehost, and final separate-process evidence incomplete. Do not duplicate that harness in System26.

When System25 supplies authoritative evidence, check T26-043/T26-053, complete T26-054 metadata and `open -> closed`, merge current `origin/master`, push `fixes/agent-8`, open/update the PR, enable auto-merge, and monitor the required `affected` gate until merged.
