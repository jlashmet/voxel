# Tasks — Kentridge macro-world physical realization

## Required feature work
- [x] Inspect the assigned feature and confirm `captures: []` / zero marked regions.
- [x] Audit the existing top-down macro graph, deterministic planner, one-shot build selection, and current neutral-marker/Manhattan-road voxel realization.
- [x] Add reusable WorldBuilder macro-region intent covering water body, mountain/ridge barrier, valley/pass, plains/meadow, forest/woodland, and generic region kinds.
- [x] Add deterministic region extents/elevation intent plus semantic relationships/constraints usable by placement and routing (`between`, `adjacent`, `contains`, `separates`, pass/crossing/through/around intent or equivalent).
- [x] Add deterministic terrain-aware hard-route solving that respects settlement envelopes, water/barriers, slope limits, and explicit semantic crossings/passes; reject an impossible blocked hard route without a solution.
- [x] Realize every macro settlement physically; preserve richer existing Kentridge/Hightown generation and add reusable grounded >=4-building blockouts, internal circulation, and road arrival/exit for currently unrealized settlements.
- [x] Emit continuous walkable road/trail surfaces for every verified hard route all the way into settlement envelopes rather than ending at neutral markers.
- [x] Author the first Kentridge macro geography pass through shared definitions: substantial lake, substantial mountain/ridge, differentiated countryside regions, and hard routes affected by geography.
- [x] Keep ecology species/density separate while exposing/querying regional kinds/exclusions for later ecology composition, matching the feature's explicit ecology non-goal.
- [x] Keep generation/streaming deterministic and compatible with voxel streaming/LOD; remote macro content remains bounded feature definitions/explicit placements rather than eager GameObjects.
- [x] Discovered: replace the prototype water-only surface repaint with a bounded streamed basin that carves physical depth and fills it with the engine's existing non-solid water material.
- [x] Discovered: add a strict production-road rise assertion (<= 6 voxels per 30 dm route step) on the exact Kentridge macro plan rather than relying only on the generic planner ceiling.
- [x] Discovered: trace the exact playable composition and prove `KentridgeDefinition.Build` selects the semantic macro layout before `KentridgeCombinedVoxelCatalogue` consumes it one-shot.
- [x] Discovered: assert the production combined catalogue contains macro roads, all four remote settlement blockouts, ridge geography, and the carved-water pass after composition.

## Behavioral regression
- [x] Add one final targeted PlayMode acceptance test that nests the full macro realization regression plus production water/slope/composition assertions: `VoxelEngine.Tests.PlayMode.KentridgeMacroWorldPhysicalProductionAcceptanceTests.PhysicalMacroWorldHasWalkableRoutesAndADeepStreamedWaterBody`.
- [ ] Green exact-SHA execution verifies fixed-seed macro-to-physical output is deterministic.
- [ ] Green exact-SHA execution verifies every `Settlement` node has a physical settlement plan and >=4 non-overlapping grounded blockout buildings when no richer generator owns it.
- [ ] Green exact-SHA execution verifies every settlement is reachable from Kentridge over contiguous generated hard-route surfaces.
- [ ] Green exact-SHA execution verifies road plans satisfy strict production slope/obstacle constraints and connect settlement arrival/exit zones.
- [ ] Green exact-SHA execution verifies lake/ridge region constraints alter production hard routes using explicit semantic solutions.
- [ ] Green exact-SHA execution verifies an impossible blocked hard route is rejected unless an explicit crossing/pass solution is authored.
- [ ] Green exact-SHA execution verifies existing Kentridge/Hightown richer output is preserved rather than replaced by generic blockouts.
- [ ] Green exact-SHA execution verifies the selected macro graph survives the real combined production catalogue, including the carved water pass.

## Visual / runtime evidence
- [ ] Exact built `KentridgePlayableSlice` reaches a usable rendered state with no startup/runtime exceptions.
- [ ] Durable player-height evidence shows continuous roads leaving/entering settlements.
- [ ] Durable closer evidence shows Moordell, Rossdam, Fairy Village, and Orc Village as physical blockout settlements.
- [ ] Durable elevated/survey evidence shows the physical road network corresponds to the semantic macro graph.
- [ ] Durable evidence shows a substantial generated lake and mountain/ridge plus a route responding to one constraint.
- [ ] Normal CharacterMotor traversal succeeds on representative generated road segments with collision/streaming active.
- [ ] Inspect the generated Rossdam basin in built-player evidence for readable shoreline/depth and no collision/rendering anomaly from the non-solid water material.

## Blast radius / cost
- [ ] Quantify macro plan counts, route-solving work, feature/placement/primitive cost, carved-water cost, and one-shot build cost from the exact green run.
- [ ] Inspect built-player CPU/GPU/memory/streaming/far-field evidence against existing device budgets; do not weaken budgets.
- [x] Static blast-radius review: macro realization is opt-in through the existing one-shot `TopDownWorldLayoutSelection`; ordinary Kentridge catalogue callers retain prior cost, and no second graph/static destination hierarchy was introduced.
- [ ] Re-check final branch diff after CI/promotion and verify no unrelated assignment/capture or `.github/test-request.json` change exists on `fixes/agent-6`.

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
