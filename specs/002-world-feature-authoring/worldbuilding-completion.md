# Worldbuilding Implementation Completion Audit

**Branch:** `agent/worldbuilding-structures-caves`  
**Scope:** `specs/002-world-feature-authoring/worldbuilding-plan.md`  
**Validation status:** source audit only; no Unity run and no local compile command was executed in the current environment.

## What is complete

The branch now has one reusable deterministic authoring stack rather than per-archetype geometry systems:

- shared integer structure component/config contracts for footprint/foundation, walls, floors, openings,
  roofs, stairs, towers, columns, buttresses, battlements, vertical accents, interiors, circulation,
  courtyards/open space, semantic material roles, cardinal transforms and attachment anchors;
- reusable house authoring and shape-program compilation with compatibility and materially different
  presets;
- castle composition over shared components, including keep, curtain, towers, gatehouse, courtyard,
  moat/ditch hooks and underground attachment semantics;
- one generic cave authoring path for standalone, underground and structure-attached caves;
- shed, church, cathedral and temple archetypes composed from shared components;
- production Kentridge settlement composition using deterministic lot/frontage policy and a weighted
  shared-house preset palette rather than a second city planner;
- versioned preset IDs, first-failure authoring diagnostics, and transient structure/settlement
  inspection APIs;
- updated quickstart, authoring examples, and normative worldbuilding authoring contract.

## Cave reuse outcome

Castle cave generation no longer owns tunnel/network mechanics. `CastleCaveAuthoring` constructs a
castle compatibility `CaveConfig`, derives a stable salted cave seed, creates an ordinary generic
`CaveGenerationRequest`, and calls `VoxelEngine.Structures.Runtime.CaveAuthoring`.

The generic cave path supports:

- standalone, structure-attached and underground entrances through the same runtime entry point;
- integer-only turn/vertical/branch/chamber decisions;
- bounded main and branch segment counts;
- deterministic chamber shape/radius/height selection;
- bounded roughness;
- explicit semantic material/decor/resource/water hooks;
- local generation bounds and world-coordinate overflow rejection;
- an explicit proof that entrance width/height/clearance remains inside the declared cave envelope.

Loops remain deliberately unsupported. `CaveConfig.EnableLoops == true` is invalid until a bounded,
region-local deterministic reconnection contract exists.

## Compatibility and call-site migration

### House

`HouseProgramCompiler` is the shared shape-program realization for ordinary houses. The compatibility
compiler retains the historical cottage opcode ordering. The showcase detailed farmhouse also uses
`HouseConfig -> HouseProgramCompiler -> FeatureCatalogue`, so the showcase is not a private house
builder.

Known limitation: the general shape-program compiler currently supports flat/gable/shed roof
realization but rejects hip roofs. Dormer/exterior/interior configs are authoring hooks; not every
hook is emitted by the current shape-program compiler yet.

### Castle

Castle composition retains compatibility presets while its underground cave path has been fully
redirected to the generic cave generator. Castle-specific code owns castle semantics, anchor choice,
palette and compatibility values; cave tunnel/network mechanics are no longer duplicated there.

### Settlement / Kentridge

The production settlement planner remains `Packages/com.mountingforce.worldgen`.
`KentridgeCombinedVoxelCatalogueCanonical` includes `KentridgeSharedStructureVoxelCatalogue` as the
building stage. Stable Kentridge role IDs, authored street/plaza topology, plot positions, frontage,
explicit placement and gameplay identity remain unchanged.

Generated residential/market forms select versioned shared house preset IDs through
`SettlementCompositionPolicy.Palette` using settlement seed + stable role ID + archetype + district,
then compile the resulting `HouseConfig` through `HouseProgramCompiler`.

Bespoke Kentridge landmarks still use their existing bounded shape-program implementations. Church,
cathedral and temple currently have immediate `IStructureAuthoringSession` authorers but no shared
shape-program compiler, so forcing those into the Kentridge catalogue would create a second
conversion path. A future increment can add shared landmark shape-program compilers and then migrate
those bespoke programmes deliberately.

The abandoned `Game.Structures` parallel-city experiment was retired; executable planner/test stubs
have been removed as encountered. Any residual metadata-only remnants are cleanup-only and are not
part of the production assembly graph.

## Integer / CPU authority audit (WB106)

The newly introduced authoritative generation paths audited here are integer CPU paths:

- `HouseProgramCompiler` emits integer shape-program opcodes and material bytes;
- `CaveNetworkAuthoringCore` uses integer vectors, integer hashes/ranges and bounded fixed lists;
- `StructureGenerationContext`, semantic child seeds and bounds are integer value types;
- church/cathedral/temple/shed/shared component authorers use integer dimensions/cardinal transforms
  through `IStructureAuthoringSession`;
- settlement lot sizing, weighted selection, density, landmark and open-space policy use integer
  ranges/hashes and stable semantic IDs;
- Kentridge shared-house realization uses integer decimetre/voxel dimensions and ordinary
  `FeatureCatalogue`/shape-program output;
- `ShapeProgram` documents and implements integer evaluation and does not read GPU-derived state.

No new authoritative structure/cave path introduced `float` state, `ComputeShader`/GPU generation, or
a GPU-derived gameplay truth. Rendering remains downstream of authoritative voxel/feature generation.

This is a source audit, not a substitute for a compile/test run.

## Bounds and budget audit (WB107 complete from source invariants)

Several source-level gaps were fixed during the completion audit:

1. `StructureGenerationContext.ForFeature` previously ignored a failed
   `StructureGenerationBounds.TryCreate`; it now rejects non-positive/overflowing footprints.
2. Cave path segments/chambers reject steps that cannot fit their local margins.
3. The cave entrance clearance carve has an explicit `EntranceFitsBounds` proof and is rejected by
   `CaveAuthoring` before any write if it exceeds the declared envelope.
4. `FeatureCatalogueBuilder.Finalise` rejects definitions whose declared footprint or declared
   `MaxPrimitives` exceeds the global hard budgets. It does not currently prove the program's true
   maximum emission count; the runtime remains fail-closed if a bad authored program escapes that
   deeper validation responsibility.
5. Production `FeatureGeneration.RasteriseInstance` validates every evaluated primitive against the
   half-open, cardinally-oriented `FeatureDefinition.Footprint` before rasterisation. Any escape
   returns `EvaluationResult.OutsideFootprint` and writes no voxels.
6. `ShapeProgram` now rejects a `ShapeOp.Repeat` count above
   `FeatureBudget.MaxPrimitivesPerInstance` with `PrimitiveLimitExceeded` instead of silently capping
   the trip count. The ordinary emit path also refuses to exceed either `definition.MaxPrimitives`
   or the global per-instance primitive limit.

Together these runtime backstops make footprint and primitive-budget failures explicit rather than
silently clipping/truncating authoritative structure generation. Compile/test execution remains
tracked separately by WB103-WB105.

## Settlement locality audit

`SettlementCompositionPolicy` requires:

- `SettlementPlanningScope.RegionLocal`;
- finite `MaxCandidatesPerRegion`;
- finite `MaxPlanningSpanDm`;
- finite lot ranges and setbacks;
- finite landmark maximum counts and minimum spacing;
- finite explicit open-space extents.

A global policy is invalid. Weighted preset choice is keyed directly by stable candidate identity and
does not consume a traversal-order-dependent random stream.

## Validation that was not run

No local checkout is mounted in the current execution environment. The repository contains
`tools/check-compile.sh`, but it could not be executed without a checkout. A connector-only branch
copy cannot execute repository scripts, and the container has no outbound DNS for cloning. Therefore
WB103 is not checked.

No Unity editor/test invocation was performed. WB104 and WB105 remain unchecked. When validation is
resumed it must use `tools/unity-run.sh` as required by the repository rules.

The mixed-city test task WB095 also remains unchecked per the current instruction to continue
implementation without spending time on tests.

## Known limitations / next increments

1. Add shared shape-program compilers for church/cathedral/temple (and other desired landmarks) before
   migrating Kentridge bespoke landmark programmes.
2. Add hip-roof realization to `HouseProgramCompiler` before advertising hip roofs on that compiler
   path; keep the config/runtime-session roof support separate until then.
3. Implement deterministic bounded cave loop/reconnection semantics if loops are still desired.
4. Resume WB095/WB103-WB105 validation when test/build execution is desired and available.

## Checklist reconciliation

Implementation through WB094 and WB096-WB102 is present. WB095 remains intentionally deferred.
WB106-WB110 can be closed from the source/compatibility audit. WB103-WB105 remain open because no
corresponding commands ran.
