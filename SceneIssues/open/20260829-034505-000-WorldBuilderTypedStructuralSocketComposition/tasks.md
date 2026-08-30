# Tasks

## Pre-implementation inventory / architecture
- [x] Confirm assignment branch starts from/refreshed with current `origin/master` and keep all work on `fixes/agent-5`.
- [x] Read `AGENTS.md`, `SceneIssues/README.md`, assignment `README.md`, `issue.json`, `plan.md`, and `tasks.md`.
- [x] Identify current assembly seams: engine-free contracts in `Game.WorldBuilder.Api`, deterministic planners in `Game.WorldBuilder.Runtime`, authoritative structure voxel authoring through `IStructureAuthoringSession`.
- [x] Discriminate the predecessor path: `VoxelEngine.Structures.Runtime.ShapeProgram.Run` actively compiles `ShapeOp.CallSlot`; complete that canonical path rather than add a second solver.
- [x] Select one canonical path: complete/reconcile `FeatureDefinition` / `SlotSpec` / `ShapeOp.CallSlot` and its production catalogue/evaluator rather than introduce a parallel `StructuralSocketComposer`.
- [x] Locate/document the active slot metadata/compiler/catalogue/hash/region chain: `SlotSpec` -> pooled `FeatureCatalogue.Slots` -> `FeatureCatalogueComposer` definition-id rebasing -> `ShapeOp.CallSlot` bytecode -> structural planner -> descendant-aware `FeatureRegionBuild`.
- [x] Audit authoring-time slot validation for every structural socket invariant; stable IDs, semantic compatibility, cardinal facing, integer bounds, clearance, capacity/cardinality, support probes, handoff consistency, cycles, call-depth, and bytecode slot references are all rejected deterministically before production use.
- [x] Ensure structural socket/slot metadata participates in deterministic catalogue/world identity so content changes cannot silently desync saves/networked generation.
- [x] Audit repository tree for a separately named `DecorationSocket*` production path: none exists on current master. Preserve existing fine-detail/attachment authoring APIs and add only a structural handoff marker; do not invent a parallel decoration solver.
- [x] Identify the concrete existing fine-detail/prop attachment consumers that must receive the structural handoff marker: `Game.Structures.Api.DecorationSpace` / `DecorationSocket` / `DecorationSocketKind` plus existing scene resolvers/adapters such as `CastleBedroomDecorationAdapter`.
- [x] Trace `ShapeProgram` bytecode walking sufficiently to extract `CallSlot` occurrences deterministically without changing primitive evaluation semantics.
- [x] Trace existing test fixtures/catalogue builders and exact built-app harness entry points before adding production code.
- [x] Preserve assembly layering: `VoxelEngine.Structures.Api/Runtime` does not depend on `Game.Structures.Api`; structural decoration-handoff metadata is engine-neutral.
- [x] Locate and reuse the production player-world traversal controller: exact `WorldbuildingGalleryShowcase` constructs `CharacterMotor`, snaps it to authoritative voxel ground, and normal/`AutoWalk` motion executes `_motor.Step(...)`.

## Canonical production contract / solver
- [x] Complete the shared authoring-validation contract: stable ids, semantic role flags/tags, cardinal facing, integer voxel transform, clearance, capacity, support probes, required/optional, support-loss invalidation, and decoration handoff are validated/preserved by the canonical catalogue and planner path.
- [x] Implement reusable structural piece/catalogue/recipe contracts with independently bounded physical extents and bounded authoring cost.
- [x] Implement deterministic compatibility predicates; require mutual type compatibility and valid opposing facing rather than string-name conventions.
- [x] Implement bounded deterministic child selection/attachment through the existing production `CallSlot` path with stable seed ordering and no generation/region-order dependence.
- [x] Enforce required/optional resolution, socket capacity/cardinality, authored clearance/child overlap, pairwise 3D spacing, terrain/structural support, and explicit failure results.
- [x] Enforce recursion/cycle protection plus runtime max depth, child count, primitive/voxel cost, and spatial extent budgets before runaway generation.
- [x] Keep composed pieces one semantic structure while child pieces retain independent bounded/streamable transforms/bounds.
- [x] Produce inspectable attachment output including semantic structure id, piece ids, socket ids, transforms, accepted links, rejected alternatives/reasons, aggregate bounds/cost, and deterministic accepted-graph hash.
- [x] Ensure accepted decisions carry support-loss invalidation plus resolved support-probe metadata for downstream destruction/support systems.
- [x] Do not leave a second competing structural-composition mechanism active.

## Authoritative voxel realization
- [x] Expand accepted child links into deterministic physical feature placements before per-region rasterization; do not inline child primitives into the root footprint.
- [x] Make region generation discover and rasterise a composed child whose explicit root footprint lies outside the requested logical region.
- [x] Filter structural roots by exact composed graph bounds/piece footprints and charge planned physical pieces against the resumable scan budget so empty-region structural scans stay bounded and interruptible.
- [x] Prove child pieces remain authoritative voxel/collision/destruction/storage content; do not realize structural children as presentation-only meshes or permanent GameObjects.
- [x] Keep each child piece bounded so monumental structures can span multiple logical generation/streaming regions without one giant feature footprint.
- [x] Adapt structural decoration handoff after realization into existing `Game.Structures` decoration consumers: the runtime adapter enumerates engine-neutral flags into existing single-kind `DecorationSocket`s while leaving fine-detail placement with the existing decoration resolver.

## Required proving cases
- [x] Monumental mountain/gorge/river bridge: two terrain/cliff anchors, multi-piece span crossing multiple logical regions, repeated explicit span/support structure, continuous walkable deck, road/traversal continuation sockets at both ends, incompatible/orientation candidate rejection.
- [x] Add production `CharacterMotor` regression traversing the entire bridge.
- [x] Castle assembly: >=2 wall runs, >=2 towers, gatehouse/gate opening, generic wall/tower/gate continuation sockets, correctly oriented tower joins, traversable entrance, incompatible roof/facade/bridge module rejection.
- [x] Multi-level cliff settlement: at least two elevations, terrain-derived supported anchors, platform/building pieces plus traversable stair/ramp/short bridge, unsupported candidate rejection.
- [x] Add production `CharacterMotor` regression traversing the gate and vertical/cliff connection where applicable.
- [x] Meso-scale facade/roof attachment: facade attachment plus roof attachment, >=2 style variants from one semantic contract, shared architecture primitives, no micro-detail socket abuse.
- [x] Integrate all four cases into deterministic built-app showcase/harness reachable from exact `Assets/Scenes/WorldbuildingGalleryShowcase.unity`.

## Regression / negative validation
- [x] Focused production-path regression proves identical seed/input => identical attachment graph/hash.
- [x] Regression proves generation/region traversal order does not alter authoritative composed child voxels.
- [x] Regression proves allowed alternate seeds produce bounded attachment variation.
- [x] Required socket with no requested/compatible child => explicit deterministic failure.
- [x] Optional socket may remain empty without failing composition.
- [x] Incompatible semantic socket/module => rejected with diagnostic reason.
- [x] Orientation/facing mismatch => rejected with diagnostic reason.
- [x] Reserved-clearance/child overlap => rejected with diagnostic reason.
- [x] Missing terrain/structural support => rejected with diagnostic reason.
- [x] Socket capacity/cardinality overflow => rejected/bounded.
- [x] Recursive/cyclic call graph and over-depth shared DAG are rejected deterministically at catalogue validation.
- [x] Runtime max-depth overflow => explicit deterministic failure before runaway generation.
- [x] Child-count / primitive-cost / voxel-cost / spatial-extent budget overflow => explicit actionable budget result without raising global budgets.
- [x] Inspection output carries semantic structure id, child piece ids, socket ids, accepted/rejected decisions, aggregate report data, and final `GraphHash`.
- [x] Regression proves structural children are emitted through production authoritative voxel authoring, including a child outside the root logical region.
- [x] Rejected alternatives remain inspectable but do not change accepted `GraphHash` identity.
- [x] Conservative voxel-authoring cost accepts the exact 16,777,216-voxel ceiling and rejects one voxel over before composition continues.
- [x] Add focused PlayMode regression over the four production gallery catalogues, deterministic graph/cost/bounds, and invalid attachment rejection.
- [ ] Validate that focused PlayMode regression and the exact-scene built-player harness in the same final exact-SHA request.

## Built-application / visual gates
- [ ] Run final targeted CI from an exact feature SHA via `ci-test/fixes/agent-5` only; never edit `.github/test-request.json` on the feature branch or replace queued/running CI.
- [ ] Exact built `WorldbuildingGalleryShowcase` presents bridge, castle, cliff/vertical assembly, and building-detail attachment in clearly navigable areas.
- [ ] Inspect every durable full-resolution built-player frame.
- [ ] Capture player-height close views of bridge/castle/cliff/detail connection seams; no gaps, overlaps, z-fighting, floating pieces, inaccessible decks or implausible supports.
- [ ] Capture wide gorge/river view proving the bridge reads monumentally between terrain masses.
- [ ] Capture whole castle assembly proving believable continuous wall/tower/gatehouse joins.
- [ ] Capture cliff settlement wide/close views proving supported multi-level attachment and accessible traversal.
- [ ] Capture both building-detail style variants at useful player-height scale.
- [ ] Built-player production `CharacterMotor` traverses full bridge, gate, and vertical connection on authoritative geometry.

## Blast radius / performance / closure
- [ ] Measure and record planning/composition time for bridge/castle.
- [ ] Measure and record child feature count, primitive/voxel-authoring cost, logical generated-region/streaming span, bounded memory-model cost, and render/triangle proxy/impact.
- [x] Reconcile composition limits with authoritative `device-matrix.md`; all six composition ceilings are deterministic and identical across tiers, with the conservative voxel model documented separately from actual region/instance raster budgets and no global budget weakened.
- [x] Confirm existing fine-detail/decoration behavior remains separate and existing unrelated generation paths are unchanged unless explicitly opted into structural composition.
- [ ] Review final feature diff for assignment-only blast radius and cost.
- [ ] Complete `issue.json` pending metadata only after all required exact-SHA workflow and built-app gates pass.
- [ ] Move open -> pending in separate bookkeeping commit per `SceneIssues/README.md`.
- [ ] After verification, move pending -> closed, set `status=fixed` and `resolvedUtc`.
- [ ] Fetch current `origin/master`, merge into `fixes/agent-5`, resolve only in-scope conflicts, push feature head, then push that exact head to `origin/master` non-force; retry merge/push if master advances.

## Discovered review work
- [x] Fix and regress shared-DAG structural call-graph depth validation so max depth is evaluated per traversal path while recursion cycles still reject deterministically.
- [x] Verify/fix child-facing compatibility under cardinal rotation while preserving explicit orientation-mismatch diagnostics.
- [x] Add malformed out-of-range `DefinitionId` regression proving deterministic rejection without out-of-range mutation/indexing.
- [x] Reconcile descendant region raster path: accepted descendants contribute authoritative region voxels/provenance and remain independently bounded.
- [x] Refresh `fixes/agent-5` with current `master` before this continuation; merge commit `3a1ad612efc77a41b307b36855c2df0dbcc76cf6` preserves both histories with no in-scope conflicts.
- [x] Enforce authored slot reserved-clearance volumes and regress deterministic overlap rejection.
- [x] Enforce declared spacing for repeated attachments pairwise in all three axes.
- [x] Add explicit bounded voxel-authoring cost to composition reports/budgets.
- [x] Carry support-loss invalidation and decoration-handoff metadata into inspectable accepted attachment decisions.
- [x] Adapt engine-neutral structural decoration handoff into existing `Game.Structures.Api.DecorationSocketKind`/`DecorationSpace` consumers without changing micro-detail semantics; combined handoff flags are regressed as distinct existing socket kinds.
- [x] Add focused negative regressions for required/optional sockets, semantic incompatibility, slot clearance, support, capacity, runtime depth, child/primitive/voxel/spatial budgets, and inspection metadata.
- [x] Add deterministic variation and generation-order regressions.
- [x] Resolve exact production player traversal component serialized in `WorldbuildingGalleryShowcase`: reuse `CharacterMotor` for bridge/gate/vertical traversal proof.
- [x] Reconcile every structural composition simulation limit with authoritative `device-matrix.md`; document deterministic cross-tier limits instead of hidden code-only budgets.
- [x] Remove managed allocations from structural support probing; current production support scan is bounded and allocation-free.
- [x] Ensure `GraphHash` represents the final accepted attachment graph while rejected alternatives remain inspectable diagnostics.
- [x] Validate/document the conservative voxel-authoring cost model against authoritative region/instance budgets and regress its exact boundary behavior.
- [x] Fix explicit-catalogue region enumeration so accepted structural descendants outside root footprints are visited and rasterized; add a production-helper regression using the existing cross-region fixture.
- [x] Ensure typed structural gallery proving content is present in both live-generate and normal bake startup modes through a bounded presence-check/compatibility repair; do not require a new bake merely to validate this feature.
- [x] Extend the existing gallery audit/player harness for feature frames, traversal assertions, and bridge/castle cost evidence; do not create a parallel validation harness.
- [x] Scope the structural audit phase to this exact SceneIssue id so unrelated capture-less WorldbuildingGallery validations keep their existing audit contract and runtime cost.
- [x] Diagnose final CI admission failure run `33306559976`: request schema used string `"60"` for `replay_seconds`, so Unity/test/player steps never ran and no gate was satisfied.
- [x] Correct `replay_seconds` to integer `60` and resubmit on the same persistent `ci-test/fixes/agent-5` transport without changing production code.
- [x] Diagnose compile-gate run `33306616999` and fix the readonly-`using` catalogue mutation errors without changing composition behavior.
- [x] Diagnose compile-gate run `33306897362`: `CharacterMotor` belongs to the SceneRuntime `VoxelEngine.Showcase` assembly, so lower-layer `ShowcaseWorld` cannot depend on it; keep route/preload data in `ShowcaseWorld` and execute traversal through a SceneRuntime extension using the existing production motor.
- [x] Diagnose compile-gate run `33307182322`: SceneRuntime audit consumes `StructuralCompositionResult` / `StructuralAttachmentRejectReason` from `VoxelEngine.Structures.Runtime`; add the existing acyclic runtime assembly reference to `VoxelEngine.Showcase.asmdef` rather than moving planner contracts or widening lower layers.
- [x] Revalidate the corrected feature source after the SceneRuntime structural-runtime reference fix; exact run `33311927310` compiled, built the standalone player, and passed the focused PlayMode regression.
- [x] Diagnose exact CI run `33308789329` from its durable artifact: bridge composition is green (`STRUCTURAL_GALLERY authored=True`, bridge result/cost emitted). The focused test actually fails later on `FixedString32Bytes` truncation for `upper-terrain-supported-platform`, while built-player structural traversal route 2 fails with ~30.9 m remaining.
- [x] Reproduce bridge terrain/support contacts for seed `0x5EED1234`; all three intended pier loci satisfy even the physical 180-voxel reach, so no additional bridge support/site change is required.
- [x] Shorten the overlength cliff proof socket name to fit its `FixedString32Bytes` contract without changing semantic identity or topology; exact run `33311927310` passed the focused PlayMode regression and authored all four built-player proof metrics without truncation.
- [x] Diagnose exact CI run `33311927310`: requested PlayMode regression passed; standalone player authored all four proofs and bridge/castle traversal passed, but route 2 stopped at `(-316.44, 24.60, 104.00)` with 6.435 m remaining. The ramp base slab fixed the initial no-voxel gap; the remaining ramp/upper-platform seam is 8 voxels (0.8 m) high versus `CharacterMotor.StepHeight=0.35m`. No structural audit frames were emitted because traversal failure occurs before capture.
- [x] Align the cliff ramp top and upper-platform walking surface by lowering the upper-platform attachment 8 voxels; preserve its stable socket ID/support contract and do not change global ramp or `CharacterMotor` semantics.
- [x] Diagnose exact CI run `33313094692`: focused PlayMode passed and route 2 crossed the corrected ramp seam, reducing 46 m to 2.318 m, but its target at `site.X + 500` lies inside the cliff house, whose footprint begins at `site.X + 480`; the production motor correctly stops at the facade.
- [x] Move the cliff traversal endpoint onto the clear upper-platform landing before the house footprint so the existing production `CharacterMotor` can complete the authored vertical route without changing motor tolerance or structural semantics; exact run `33314706183` reached within 1.317 m.
- [x] Correct the cliff/vertical built-player traversal route so production `CharacterMotor` reaches the authored upper endpoint on authoritative geometry; exact run `33314706183` passed all three routes.
- [x] Run exact mechanical checkpoint `33314706183`: focused PlayMode and standalone-player audit passed, all eight structural frames were emitted, and bridge/castle/cliff traversal plus cost metrics are mechanically green.
- [x] Wire the bounded authoritative-voxel structural presentation pass into normal `WorldbuildingGalleryShowcase` startup and the existing structural audit path; do not add a parallel visual-only composition system.
- [x] Reconcile proof-local presentation primitive ceilings with the authored bridge/cliff programs without raising any global structural budget.
- [ ] Upgrade the bridge proof and capture so the wide built-player frame clearly spans a substantial gorge/river between high terrain masses rather than reading as a slab over shallow terrain or sparse/unloaded-looking background.
- [ ] Replace placeholder bridge slab/support treatment with intentional reusable architectural hierarchy (deck edge/rail, grounded abutments/piers, span rhythm) while preserving typed structural sockets and authoritative voxel realization.
- [ ] Upgrade castle proof visuals beyond placeholder prisms with a deliberate wall/tower/gatehouse silhouette, meaningful material/detail hierarchy, believable grounded joins, and no arbitrary floating elements.
- [ ] Upgrade the cliff settlement presentation so built-player wide/close views clearly show steep multi-level supported terraces/platforms, vertical traversal, and grounded architecture without sparse/unloaded-looking terrain dominating the shot.
- [ ] Upgrade facade/roof variants from raw box masses to reusable high-detail architectural treatment with clear facade/roof hierarchy and two visibly distinct style variants through the same typed semantic contracts.
- [x] Reframe the existing structural audit captures to include useful player-height seam views and establishing context for all four proofs; keep validation in the existing harness rather than create a parallel capture path.
- [ ] Re-run final exact-SHA targeted CI after visual/content rework, inspect all durable 1600x900 structural source frames individually, and reject any placeholder-level composition before closure.
- [x] Refresh the final visual rework with current `master` in merge commit `0cd53d0761698490ad29edd563167251030b1aa5` without changing unrelated master content.
- [x] Diagnose exact final-visual run `33323693205`: both PlayMode and built-player compilation fail only because `ShowcaseWorld.WorldbuildingGalleryStructuralPresentation.cs` imports `System.Diagnostics` and uses unqualified `Debug.Log`, producing CS0104 ambiguity before tests or capture can run.
- [x] Qualify the two presentation log calls as `UnityEngine.Debug.Log` without changing structural behavior, budgets, or validation semantics; rerun the same persistent CI transport from the new exact source SHA.
