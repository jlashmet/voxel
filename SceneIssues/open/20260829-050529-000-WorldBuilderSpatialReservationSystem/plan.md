# Plan — WorldBuilder Spatial Reservation System

## Observed behavior / acceptance

WorldBuilder spatial ownership is fragmented across settlement placement, roads, architecture, ecology and underground generation. This feature must provide one deterministic integer-space claim/query substrate without becoming a global first-writer registry or replacing route, socket, ecology or cave policy. Closure requires production Kentridge, road/macro, architecture, vegetation and underground consumers; stable diagnostics; bounded cost; gallery inspection; exact-SHA CI; and built-player evidence.

## Discriminators and results

Hypothesis A: a reusable production reservation/precedence service already exists and this work should adapt it. **Falsified** on current `master`; no competing canonical claim service exists.

Hypothesis B: the open typed-`StructuralSocket` feature is required before structural integration. **Falsified** for this assignment: current `master` still has no typed socket. The existing production boundary is `StructureSiteGeometry` plus architecture-resolved child/form extents. Reservations will validate only spatial clearance; compatibility/orientation/support remain architecture-owned.

Road audit result: `WorldRoadNetwork` is now canonical for resolved road geometry, shoulders and clearance. Reservation adapters must consume it directly and remove inferred production-road approximations.

Inspection audit result: `WorldbuildingGalleryAuditHarness` is the existing built-player gallery hook. It will consume snapshots read-only; any debug GameObjects are presentation only and will have no authoritative colliders/state.

## Selected implementation

1. Keep the engine-free Core reservation contract already implemented: stable ids/provenance, 3D boxes/corridors, hard/clearance/protected/handoff/soft semantics, deterministic precedence, bounded snapshot buckets, planner-local replay/release, diagnostics and query metrics.
2. Finish production adapters: canonical `WorldRoadNetwork` + top-down settlement envelope/public arrival, architecture site/child clearance, caller-owned vegetation snapshot, and real hidden-space snapshot.
3. Add focused production regressions for road/macro handoff, structure clearance/connector compatibility, vegetation suppression/device independence, hidden-space 3D behavior, regeneration/order stability, and build/query work metrics.
4. Extend the existing gallery audit path with non-authoritative surface/underground/rejected-candidate reservation visualization and durable cost/diagnostic logging.
5. Validate compile/static diff and blast radius, then issue exactly one final request through `ci-test/fixes/agent-7` using the repository-required PlayMode SceneIssue transport so focused regression and standalone-player validation run against the exact feature SHA.
6. Only after every task and acceptance criterion is green: complete pending metadata, move open -> pending -> closed, set `status=fixed`/`resolvedUtc`, merge current `origin/master`, and promote the exact feature head to `origin/master` non-force.

## Risk / budgets

Preserve Kentridge fixed candidate ordering and 256-attempt cap. Do not duplicate road geometry math, eagerly materialize world claims, use Unity Physics as authority, weaken device budgets, or create per-claim authoritative GameObjects. Measure snapshot construction, bucket/candidate/narrow-phase work, allocations/memory where the harness supports it, and generation/regeneration impact before closure.
