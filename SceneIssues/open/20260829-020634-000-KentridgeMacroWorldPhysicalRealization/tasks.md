# Tasks — Kentridge macro-world physical realization

## Required feature work
- [x] Inspect the assigned feature and confirm `captures: []` / zero marked regions.
- [x] Audit the existing top-down macro graph, deterministic planner, one-shot build selection, and current neutral-marker/Manhattan-road voxel realization.
- [ ] Add reusable WorldBuilder macro-region intent covering water body, mountain/ridge barrier, valley/pass, plains/meadow, forest/woodland, and generic region kinds.
- [ ] Add deterministic region extents/elevation intent plus semantic relationships/constraints usable by placement and routing (`between`, `adjacent`, `contains`, `separates`, pass/crossing/through/around intent or equivalent).
- [ ] Add deterministic terrain-aware hard-route solving that respects settlement envelopes, water/barriers, slope limits, and explicit semantic crossings/passes; reject an impossible blocked hard route without a solution.
- [ ] Realize every macro settlement physically; preserve richer existing Kentridge/Hightown generation and add reusable grounded >=4-building blockouts, internal circulation, and road arrival/exit for currently unrealized settlements.
- [ ] Emit continuous walkable road/trail surfaces for every verified hard route all the way into settlement envelopes rather than ending at neutral markers.
- [ ] Author the first Kentridge macro geography pass through shared definitions: substantial lake, substantial mountain/ridge, differentiated countryside regions, and at least one hard route visibly affected by geography.
- [ ] Keep ecology species/density separate while exposing/querying regional kinds/exclusions for later ecology composition.
- [ ] Keep generation/streaming deterministic and compatible with voxel streaming/LOD; do not eagerly instantiate remote GameObjects or weaken device budgets.

## Behavioral regression
- [ ] Verify fixed-seed macro-to-physical output is deterministic.
- [ ] Verify every `Settlement` node has a physical settlement plan and >=4 non-overlapping grounded blockout buildings when no richer generator owns it.
- [ ] Verify every settlement is reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] Verify road plans satisfy slope/obstacle constraints and connect settlement arrival/exit zones.
- [ ] Verify lake/ridge region constraints alter at least one production hard route using an explicit semantic solution.
- [ ] Verify an impossible blocked hard route is rejected unless an explicit crossing/pass solution is authored.
- [ ] Verify existing Kentridge/Hightown richer output is preserved rather than replaced by generic blockouts.

## Visual / runtime evidence
- [ ] Exact built `KentridgePlayableSlice` reaches a usable rendered state with no startup/runtime exceptions.
- [ ] Durable player-height evidence shows continuous roads leaving/entering settlements.
- [ ] Durable closer evidence shows Moordell, Rossdam, Fairy Village, and Orc Village as physical blockout settlements.
- [ ] Durable elevated/survey evidence shows the physical road network corresponds to the semantic macro graph.
- [ ] Durable evidence shows a substantial generated lake and mountain/ridge plus a route responding to one constraint.
- [ ] Normal CharacterMotor traversal succeeds on representative generated road segments with collision/streaming active.

## Blast radius / cost
- [ ] Quantify macro plan counts, route-solving work, feature/placement/voxel cost, and one-shot build cost.
- [ ] Inspect built-player CPU/GPU/memory/streaming/far-field evidence against existing device budgets; do not weaken budgets.
- [ ] Review all shared consumers and verify no second graph, Kentridge-only direct voxel writing, giant static scene, or unrelated assignment/capture change.

## Acceptance audit / closure
- [ ] Acceptance (1): existing source-backed macro graph remains authoritative and is consumed through reusable WorldBuilder/shared APIs.
- [ ] Acceptance (2): every settlement has physical presence with required blockout quality or richer existing generation.
- [ ] Acceptance (3): every settlement is physically reachable from Kentridge through contiguous generated hard-route travel surfaces.
- [ ] Acceptance (4): roads are terrain-aware and require semantic solutions for blocked geography.
- [ ] Acceptance (5): reusable geographic-region authoring/query capability covers required region kinds, extents/elevation, relationships, deterministic variation, terrain output, and constraints.
- [ ] Acceptance (6): built macro world contains substantial lake + ridge and at least one geography-constrained hard route.
- [ ] Acceptance (7): regional terrain reads as differentiated countryside rather than flat debug space.
- [ ] Acceptance (8): no scene-local second graph/direct voxel-writing/static destination hierarchy is introduced.
- [ ] Acceptance (9): focused production-path regressions cover deterministic realization, reachability, roads, settlements, constraints, and blocked-route failure.
- [ ] Acceptance (10): exact built-player visual/runtime evidence covers settlements, roads, geography, constrained route, and CharacterMotor traversal.
- [ ] Acceptance (11): blast radius and world-build/route/CPU/GPU/memory/streaming cost are measured against budgets.
- [ ] Every checkbox above is complete before `open -> pending`.
- [ ] Final exact-SHA focused CI and built-player evidence are green.
- [ ] Complete pending metadata and move only this feature `open -> pending`.
- [ ] Move only this feature `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-6`, push exact feature head, then non-force push that head to `origin/master`; retry if master advances.
