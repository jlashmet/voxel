# Tasks

## Pre-implementation inventory / architecture
- [x] Confirm assignment branch starts from current `origin/master` and keep all work on `fixes/agent-5`.
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, assignment `README.md`, `issue.json`, `plan.md`, and `tasks.md`.
- [x] Identify current assembly seams: engine-free contracts in `Game.WorldBuilder.Api`, deterministic planners in `Game.WorldBuilder.Runtime`, authoritative structure voxel authoring through `IStructureAuthoringSession`.
- [x] Discriminate the predecessor path: `VoxelEngine.Structures.Runtime.ShapeProgram.Run` actively compiles `ShapeOp.CallSlot` but its production switch case is a no-op. The prior "no active CallSlot" inventory was falsified.
- [x] Select one canonical path: complete/reconcile `FeatureDefinition` / `SlotSpec` / `ShapeOp.CallSlot` and its production catalogue/evaluator rather than introduce a parallel `StructuralSocketComposer`.
- [x] Locate/document the active slot metadata/compiler/catalogue/hash/region chain: `SlotSpec` -> pooled `FeatureCatalogue.Slots` -> `FeatureCatalogueComposer` definition-id rebasing -> `ShapeOp.CallSlot` bytecode -> current no-op evaluator -> top-level-only `FeatureRegionBuild`; catalogue hash currently omits slots.
- [ ] Audit/extend authoring-time slot validation and prove generation-order independence after composition is active.
- [ ] Ensure structural socket/slot metadata participates in deterministic catalogue/world identity so content changes cannot silently desync saves/networked generation.
- [x] Audit repository tree for a separately named `DecorationSocket*` production path: none exists on current master. Preserve existing fine-detail/attachment authoring APIs and add only a structural handoff marker; do not invent a parallel decoration solver.
- [x] Identify the concrete existing fine-detail/prop attachment consumers that must receive the structural handoff marker: `Game.Structures.Api.DecorationSpace` / `DecorationSocket` / `DecorationSocketKind` plus existing scene resolvers/adapters such as `CastleBedroomDecorationAdapter`; structural composition emits engine-neutral handoff data and Game.Structures maps it into these prop sockets.
- [x] Trace `ShapeProgram` bytecode walking sufficiently to extract `CallSlot` occurrences deterministically without changing primitive evaluation semantics: every instruction is `[opcode][modeMask][operands...]`; `CallSlot` has one `slotIndex` operand and `ShapeOps.InstructionLength` supplies the canonical decoder length.
- [x] Trace existing test fixtures/catalogue builders and exact built-app harness entry points before adding production code: `Assets/Game/Structures/Tests/Game.Structures.Tests.asmdef`, `WorldbuildingVisualRegressionTests`, `VisualStructureCapture`, and exact `Assets/Scenes/WorldbuildingGalleryShowcase.unity` are the existing regression/visual surfaces.
- [ ] Preserve assembly layering: `VoxelEngine.Structures.Api/Runtime` must not depend on `Game.Structures.Api`; define structural decoration-handoff metadata engine-neutrally and adapt it to `DecorationSpace` / `DecorationSocketKind` from the Game.Structures layer.
- [ ] Locate and reuse the production `CharacterMotor` traversal harness rather than inventing a test-only mover.

## Canonical production contract / solver
- [ ] Generalize the existing slot contract into one shared structural socket contract: stable ids, semantic role flags/tags, cardinal facing, integer voxel position/transform, clearance volume, attachment capacity, support requirements/probes, required/optional semantics, support-loss invalidation metadata, and decoration handoff metadata.
- [ ] Implement reusable structural piece/catalogue/recipe contracts with independently bounded physical extents and bounded authoring cost.
- [ ] Implement deterministic compatibility predicates; require mutual type compatibility and valid opposing facing rather than string-name conventions.
- [ ] Implement bounded deterministic child selection/attachment through the existing production `CallSlot` path with stable seed ordering and no generation-order dependence.
- [ ] Enforce required/optional resolution, socket capacity/cardinality, clearance/overlap, footprint/spacing, terrain/structural support, and explicit failure results.
- [ ] Enforce recursion/cycle protection plus max depth, child count, primitive/voxel cost, and spatial extent budgets before runaway generation.
- [ ] Keep composed pieces one semantic structure while child pieces retain independent bounded/streamable transforms/bounds.
- [ ] Produce immutable/inspectable attachment graph output including semantic structure id, piece ids, socket ids, transforms, accepted links, rejected alternatives/reasons, aggregate bounds/cost, and deterministic graph hash.
- [ ] Ensure destruction/support-loss metadata can identify attachment points that should invalidate when supporting voxels are lost.
- [ ] Do not leave a second competing structural-composition mechanism active.

## Authoritative voxel realization
- [ ] Expand accepted child links into deterministic physical feature placements before per-region rasterization; do not inline child primitives into the root footprint.
- [ ] Prove child pieces remain authoritative voxel/collision/destruction/storage content; do not realize structural children as presentation-only meshes or permanent GameObjects.
- [ ] Keep each child piece bounded so monumental structures can span multiple logical generation/streaming regions without one giant feature footprint.
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
- [ ] Focused production-path regression proves identical seed/input => identical attachment graph/hash.
- [ ] Regression proves allowed alternate seeds/styles produce meaningful bounded variation.
- [ ] Required socket with no compatible candidate => explicit deterministic failure.
- [ ] Optional socket may remain empty without failing composition.
- [ ] Incompatible semantic socket/module => rejected with diagnostic reason.
- [ ] Orientation/facing mismatch => rejected with diagnostic reason.
- [ ] Reserved-clearance/child overlap => rejected with diagnostic reason.
- [ ] Missing terrain/structural support => rejected with diagnostic reason.
- [ ] Socket capacity/cardinality overflow => rejected/bounded.
- [ ] Recursive/cyclic recipe and max-depth overflow => rejected before runaway generation.
- [ ] Child-count / primitive-voxel-cost / spatial-extent budget overflow => explicit actionable budget result; do not raise global budgets.
- [ ] Inspection output includes semantic structure id, child piece ids, socket ids, compatibility decisions/rejections, final graph and graph hash.
- [ ] Regression proves structural children are emitted through production authoritative voxel authoring.

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
