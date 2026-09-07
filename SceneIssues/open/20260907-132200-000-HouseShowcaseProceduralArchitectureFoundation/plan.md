# HouseShowcase procedural architecture foundation

## Observed state and acceptance

Baseline source `d89495bcc` already used real voxel Storage and production Rendering. `HouseShowcase` consumed `GuildHousePrototypeComposition` directly while `Game.Structures` already owned guild programs, deterministic spatial planning, production voxel authoring, region-driven materials, roofs/openings/foundations, and guild-house validation. The missing foundation was a reusable architecture registration/config/review contract and a generic HouseShowcase consumer.

Acceptance is the full issue note: semantic profile + provider/archetype + seed + explicit reusable parameters; deterministic regeneration and meaningful seed variation; production voxel/material/texture/SDF-compatible realization with traversal/collision; HouseShowcase exterior/interior review controls; data-driven references and deterministic review poses; module-local built-player validation; four valid seeds; direct image review; Kentridge integration; concise extension documentation. Furniture/final town art/world placement remain out of scope.

## Ownership and selected approach

`Assets/Game/Structures` owns shared architecture contracts, provider registry, deterministic generation descriptors/results, and the guild-house provider adaptation. It is runtime/player-visible and owns `Assets/Game/Structures/Validation/ArchitectureFoundationRuntimeValidation.unity` plus focused tests. `Assets/Game/Composition/Showcase/SceneRuntime` owns HouseShowcase selection/regeneration/review/reference composition and owns `Validation/HouseShowcaseArchitectureValidation.unity`. `Assets/Scenes/HouseShowcase` remains integration only. Production geometry/material/texture/SDF/collision authority stays in its existing owning modules.

H1: a new architecture engine is required. H2: existing guild-house production generation is reusable if exposed behind semantic provider/config contracts. Inspection and implementation support H2. The feature adds generic contracts/registry and an adapter over existing guild generation; it does not fork geometry, materials, Storage, rendering, or collision.

## Implemented foundation

Current implementation head before validation bookkeeping is `00d850adabb0cfef1ef80a81f9174ef609203a41`.

- `ArchitectureModel`, `ArchitectureRegistry`, `ArchitectureReviewRegistry`, and durable review metadata provide semantic profile/provider/archetype discovery, deterministic request/result identity, explicit parameter support, production capability metadata, reference descriptors, and deterministic exterior/interior poses.
- `GuildHouseArchitectureProvider` adapts existing guild composition/authoring. It supports footprint, room count, storey count, and floor height; unsupported roof/foundation/opening/trim/detail overrides are rejected explicitly.
- Existing guild authoring now honors multi-storey lodge shells, reserves stairwells, authors occupancy-backed stair flights, carves real window apertures with production Glass, and uses the existing curved primitive/rasterizer path for signed-distance canopy presentation.
- `ICurvedStructureAuthoringSession` is a narrow capability extension; `StructureAuthoringSession` implements it through the existing production curved primitive emitter/rasterizer, preserving boundary samples and write budgeting.
- Focused Structures tests cover registration/reuse, deterministic identity, four seed-driven massing variants, invalid parameter rejection, topology/circulation, real window operations, materials and curved/SDF authoring.
- Structures and Showcase each own focused production-path validation scenes/scenarios.
- `HouseShowcase` is now registry/provider-driven, exposes profile/provider/archetype/seed/supported parameters, same-input regeneration, distinct-seed generation, deterministic exterior/interior review, free-fly inspection, and durable reference/runtime pair metadata. Furnishing controls were removed from this feature path.
- Neutral reference art and `Documentation/ProceduralArchitectureFoundation.md` document the review and extension boundary.

## Blast radius, budgets, and current blocker

The branch is currently ahead of `master` and not behind it. The source diff is scoped to architecture contracts/provider/tests/validation, the HouseShowcase consumer, one narrow engine curved-authoring capability, neutral reference/documentation, and the assigned SceneIssue. No Kentridge runtime source is modified.

Production authoring retains bounded budgets: HouseShowcase uses an 8,000,000-write structure session; the four-variant module validation uses 20,000,000 writes and fails on budget exhaustion; Storage/render lifecycle is explicitly cleared/disposed on rebuild/disable. Registries reject duplicate keys rather than accumulating per-regeneration registrations. Shared APIs contain semantic keys rather than raw material IDs or scene coordinates; material IDs remain inside the production guild adapter/authoring and composition reference/camera constants remain Showcase-owned.

The coordinator-specified exact targeted-CI request `82808e2340f8c467ac4d29a58b0e01e391976143` / run `34165897954` remains queued with no runner assigned and must not be replaced while active. Independent implementation work is complete enough for validation, but no execution/image/Kentridge acceptance is claimed from source inspection alone.

## Remaining gates

1. Let the existing exact request finish without replacement; inspect its result/evidence.
2. On the final feature head, run the required exact-SHA targeted CI. Repository-derived module validation must execute the Structures and Showcase module-local players; focused EditMode coverage must pass with nonzero tests/captures.
3. Run/retain exact-source HouseShowcase built-player evidence at 1600x900: canonical exterior/interior/reference pair, three additional deterministic seeds, configuration/profile switching, and direct visual inspection of every required image.
4. Run the established Kentridge integration target `Assets/Game/Composition/Kentridge/Playable/Validation/KentridgeEncounterRealizationValidation.unity`; final PR `affected` must also pass canonical standalone `KentridgePlayableSlice` as required by repository workflow.
5. Resolve any product failures on `fixes/agent-1`; retry only proven infrastructure failures.
6. After every acceptance item is proven, finish issue evidence/checklist, move open→closed, set fixed/resolved metadata, merge current master, open/update the feature PR, enable auto-merge, pass `affected`, and confirm the closed issue on `origin/master`.
