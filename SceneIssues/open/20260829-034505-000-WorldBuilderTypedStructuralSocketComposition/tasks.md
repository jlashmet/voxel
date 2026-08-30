# Tasks

## Pre-implementation inventory / architecture
- [x] Confirm assignment branch starts from current `origin/master` and keep all work on `fixes/agent-5`.
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, assignment `README.md`, `issue.json`, `plan.md`, and `tasks.md`.
- [x] Identify current assembly seams: engine-free contracts in `Game.WorldBuilder.Api`, deterministic planners in `Game.WorldBuilder.Runtime`, authoritative structure voxel authoring through `IStructureAuthoringSession`.
- [x] Discriminate the predecessor path: `VoxelEngine.Structures.Runtime.ShapeProgram.Run` actively compiles `ShapeOp.CallSlot` but its production switch case is a no-op. The prior "no active CallSlot" inventory was falsified.
- [x] Select one canonical path: complete/reconcile `FeatureDefinition` / `SlotSpec` / `ShapeOp.CallSlot` and its production catalogue/evaluator rather than introduce a parallel `StructuralSocketComposer`.
- [x] Locate/document the active slot metadata/compiler/catalogue/hash/region chain: `SlotSpec` -> pooled `FeatureCatalogue.Slots` -> `FeatureCatalogueComposer` definition-id rebasing -> `ShapeOp.CallSlot` bytecode -> structural planner -> descendant-aware `FeatureRegionBuild`.
- [ ] Audit/extend authoring-time slot validation and prove generation-order independence after composition is active.
- [x] Ensure structural socket/slot metadata participates in deterministic catalogue/world identity so content changes cannot silently desync saves/networked generation.
- [x] Audit repository tree for a separately named `DecorationSocket*` production path: none exists on current master. Preserve existing fine-detail/attachment authoring APIs and add only a structural handoff marker; do not invent a parallel decoration solver.
- [x] Identify the concrete existing fine-detail/prop attachment consumers that must receive the structural handoff marker: `Game.Structures.Api.DecorationSpace` / `DecorationSocket` / `DecorationSocketKind` plus existing scene resolvers/adapters such as `CastleBedroomDecorationAdapter`; structural composition emits engine-neutral handoff data and Game.Structures maps it into these prop sockets.
- [x] Trace `ShapeProgram` bytecode walking sufficiently to extract `CallSlot` occurrences deterministically without changing primitive evaluation semantics: every instruction is `[opcode][modeMask][operands...]`; `CallSlot` has one `slotIndex` operand and `ShapeOps.InstructionLength` supplies the canonical decoder length.
- [x] Trace existing test fixtures/catalogue builders and exact built-app harness entry points before adding production code: `Assets/Game/Structures/Tests/Game.Structures.Tests.asmdef`, `WorldbuildingVisualRegressionTests`, `VisualStructureCapture`, and exact `Assets/Scenes/WorldbuildingGalleryShowcase.unity` are the existing regression/visual surfaces.
- [x] Preserve assembly layering: `VoxelEngine.Structures.Api/Runtime` does not depend on `Game.Structures.Api`; structural decoration-handoff metadata is engine-neutral.
- [ ] Locate and reuse the production player-world traversal controller used by `WorldbuildingGalleryShowcase` rather than inventing a test-only mover; assignment names this acceptance harness `CharacterMotor`.

## Canonical production contract / solver
- [ ] Generalize the existing slot contract into one shared structural socket contract: stable ids, semantic role flags/tags, cardinal facing, integer voxel position/transform, clearance volume, attachment capacity, support requirements/probes, required/optional semantics, support-loss invalidation metadata, and decoration handoff metadata.
- [x] Implement reusable structural piece/catalogue/recipe contracts with independently bounded physical extents and bounded authoring cost.
- [x] Implement deterministic compatibility predicates; require mutual type compatibility and valid opposing facing rather than string-name conventions.
- [ ] Implement bounded deterministic child selection/attachment through the existing production `CallSlot` path with stable seed ordering and no generation-order dependence.
- [ ] Enforce required/optional resolution, socket capacity/cardinality, clearance/overlap, footprint/spacing, terrain/structural support, and explicit failure results.
- [ ] Enforce recursion/cycle protection plus max depth, child count, primitive/voxel cost, and spatial extent budgets before runaway generation.
- [x] Keep composed pieces one semantic structure while child pieces retain independent bounded/streamable transforms/bounds.
- [ ] Produce immutable/inspectable attachment graph output including semantic structure id, piece ids, socket ids, transforms, accepted links, rejected alternatives/reasons, aggregate bounds/cost, and deterministic graph hash.
- [ ] Ensure destruction/support-loss metadata can identify attachment points that should invalidate when supporting voxels are lost.
- [x] Do not leave a second competing structural-composition mechanism active.

## Authoritative voxel realization
- [x] Expand accepted child links into deterministic physical feature placements before per-region rasterization; do not inline child primitives into the root footprint.
- [x] Make region generation discover and rasterise a composed child whose explicit root footprint lies outside the requested logical region.
- [x] Filter structural roots by exact composed graph bounds/piece footprints and charge planned physical pieces against the resumable scan budget so empty-region structural scans stay bounded and interruptible.
- [x] Prove child pieces remain authoritative voxel/collision/destruction/storage content; do not realize structural children as presentation-only meshes or permanent GameObjects.
- [x] Keep each child piece bounded so monumental structures can span multiple logical generation/streaming regions without one giant feature footprint.
- [ ] Preserve/expose decoration handoff spaces after structural realization without moving micro-detail responsibilities into structural sockets.

## Required proving cases
- [ ] Monumental mountain/gorge/river bridge: two terrain/cliff anchors, multi-piece span crossing multiple logical regions, repeated explicit span/support structure, continuous walkable deck, road/traversal continuation sockets at both ends, incompatible/orientation candidate rejection.
- [ ] Add production CharacterMotor regression traversing the entire bridge.
- [ ] Castle assembly: >=2 wall runs, >=2 towers, gatehouse/gate opening, generic wall/tower/gate continuation sockets, correctly oriented tower joins, traversable entrance, incompatible roof/facade/bridge module rejection.
- [ ] Multi-level cliff settlement: at least two elevations, terrain-derived supported anchors, platform/building pieces plus traversable stair/ramp/short bridge, unsupported candidate rejection.
- [ ] Add production CharacterMotor regression traversing the gate and vertical/cliff connection where applicable.
- [ ] Meso-scale facade/roof attachment: facade attachment plus roof attachment, >=2 style variants from one semantic contract, shared architecture primitives, no micro-detail socket abuse.
- [ ] Integrate all four cases into deterministic built-app showcase/harness reachable from exact `Assets/Scenes/WorldbuildingGalleryShowcase.unity`.

## Regression / negative validation
- [x] Focused production-path regression proves identical seed/input => identical attachment graph/hash.
- [ ] Regression proves generation/region traversal order does not alter the attachment graph/hash or authoritative voxels.
- [ ] Regression proves allowed alternate seeds/styles produce meaningful bounded variation.
- [ ] Required socket with no compatible candidate => explicit deterministic failure.
- [ ] Optional socket may remain empty without failing composition.
- [ ] Incompatible semantic socket/module => rejected with diagnostic reason.
- [x] Orientation/facing mismatch => rejected with diagnostic reason.
- [ ] Reserved-clearance/child overlap => rejected with diagnostic reason.
- [ ] Missing terrain/structural support => rejected with diagnostic reason.
- [ ] Socket capacity/cardinality overflow => rejected/bounded.
- [x] Recursive/cyclic call graph and over-depth shared DAG are rejected deterministically at catalogue validation.
- [ ] Runtime max-depth overflow => explicit deterministic failure before runaway generation.
- [ ] Child-count / primitive-voxel-cost / spatial-extent budget overflow => explicit actionable budget result; do not raise global budgets.
- [ ] Inspection output includes semantic structure id, child piece ids, socket ids, compatibility decisions/rejections, final graph and graph hash.
- [x] Regression proves structural children are emitted through production authoritative voxel authoring, including a child outside the root logical region.

## Built-application / visual gates
- [ ] Run final targeted CI from an exact feature SHA via `ci-test/fixes/agent-5` only; never edit `.github/test-request.json` on the feature branch or replace queued/running CI.
- [ ] Exact built `WorldbuildingGalleryShowcase` presents bridge, castle, cliff/vertical assembly, and building-detail attachment in clearly navigable areas.
- [ ] Inspect every durable full-resolution built-player frame.
- [ ] Capture player-height close views of bridge/castle/cliff/detail connection seams; no gaps, overlaps, z-fighting, floating pieces, inaccessible decks or implausible supports.
- [ ] Capture wide gorge/river view proving the bridge reads monumentally between terrain masses.
- [ ] Capture whole castle assembly proving believable continuous wall/tower/gatehouse joins.
- [ ] Capture cliff settlement wide/close views proving supported multi-level attachment and accessible traversal.
- [ ] Capture both building-detail style variants at useful player-height scale.
- [ ] Built-player CharacterMotor traverses full bridge, gate, and vertical connection on authoritative geometry.

## Blast radius / performance / closure
- [ ] Measure and record planning/composition time for bridge/castle.
- [ ] Measure and record child feature count, primitive/voxel-authoring cost, logical generated-region/streaming span, bounded memory-model cost, and render/triangle proxy/impact.
- [ ] Confirm solver budgets remain bounded/deterministic at world scale and no global feature/region/device budget is weakened.
- [ ] Confirm existing fine-detail/decoration behavior remains separate and existing unrelated generation paths are unchanged unless explicitly opted into structural composition.
- [ ] Review final feature diff for assignment-only blast radius and cost.
- [ ] Complete `issue.json` pending metadata only after all required exact-SHA workflow and built-app gates pass.
- [ ] Move open -> pending in separate bookkeeping commit per `SceneIssues/README.md`.
- [ ] After verification, move pending -> closed, set `status=fixed` and `resolvedUtc`.
- [ ] Fetch current `origin/master`, merge into `fixes/agent-5`, resolve only in-scope conflicts, push feature head, then push that exact head to `origin/master` non-force; retry merge/push if master advances.

## Discovered review work
- [x] Fix and regress shared-DAG structural call-graph depth validation so max depth is evaluated per traversal path while recursion cycles still reject deterministically.
- [x] Verify/fix child-facing compatibility under cardinal rotation so semantic compatibility does not pre-reject a child orientation that `TryChildOrientation` can validly align; preserve explicit orientation-mismatch diagnostics.
- [x] Add malformed out-of-range `DefinitionId` regression proving catalogue/planner rejection is deterministic and never indexes past `Definitions` or mutates output.
- [x] Reconcile the already-added descendant region raster path with proving cases: accepted descendants contribute authoritative region voxels/provenance and remain independently bounded across logical regions.
- [x] Refresh `fixes/agent-5` with current `master` before further implementation; merge commit `9402cc846f739723b3a98e1c0401bd0b69ea1877` preserves both histories with no in-scope conflicts.
- [ ] Enforce authored slot reserved-clearance volumes, not only child-piece clearance, and regress deterministic overlap rejection.
- [ ] Enforce declared spacing for repeated attachments in all relevant axes/pairwise placements instead of quantizing only X.
- [ ] Add explicit bounded voxel-authoring cost to composition reports/budgets; primitive cost alone does not satisfy the assignment's primitive/voxel-cost gate.
- [ ] Carry support-loss invalidation and decoration-handoff metadata into inspectable accepted attachment decisions so downstream destruction/decoration systems can consume it without re-solving.
- [ ] Adapt engine-neutral structural decoration handoff into existing `Game.Structures.Api.DecorationSocketKind`/`DecorationSpace` consumers without changing micro-detail semantics.
- [ ] Add focused negative regressions for required/optional sockets, semantic incompatibility, slot clearance, support, capacity, runtime depth, child/primitive/voxel/spatial budgets, and inspection metadata.
- [ ] Add deterministic variation and generation-order regressions.
- [ ] Resolve the exact production player traversal component serialized in `WorldbuildingGalleryShowcase` and use it for bridge/gate/vertical traversal proof.
- [ ] Reconcile every structural composition simulation limit with the authoritative `device-matrix.md`; document deterministic cross-tier limits instead of leaving hidden code-only budgets.
- [ ] Remove managed allocations from structural support probing so composition remains allocation-free/bounded on the production generation path.
- [ ] Ensure `GraphHash` represents the final accepted attachment graph while rejected alternatives remain inspectable diagnostics rather than changing the accepted-graph identity.
- [ ] Validate/document the conservative voxel-authoring cost model against the authoritative region/instance budgets and regress its boundary behavior.
