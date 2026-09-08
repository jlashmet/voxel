# HouseShowcase procedural architecture foundation

## Observed state and acceptance

Baseline source `d89495bcc` already used real voxel Storage and production Rendering. `HouseShowcase` consumed `GuildHousePrototypeComposition` directly while `Game.Structures` already owned guild programs, deterministic spatial planning, production voxel authoring, region-driven materials, roofs/openings/foundations, and guild-house validation. The missing foundation was a reusable architecture registration/config/review contract and a generic HouseShowcase consumer.

Acceptance is the full issue note: semantic profile + provider/archetype + seed + explicit reusable parameters; deterministic regeneration and meaningful seed variation; production voxel/material/texture/SDF-compatible realization with traversal/collision; HouseShowcase exterior/interior review controls; data-driven references and deterministic review poses; module-local built-player validation; four valid seeds; direct image review; Kentridge integration; concise extension documentation. Furniture/final town art/world placement remain out of scope.

## Ownership and selected approach

`Assets/Game/Structures` owns shared architecture contracts, provider registry, deterministic generation descriptors/results, and the guild-house provider adaptation. It is runtime/player-visible and owns `Assets/Game/Structures/Validation/ArchitectureFoundationRuntimeValidation.unity` plus focused tests. `Assets/Game/Composition/Showcase/SceneRuntime` owns HouseShowcase selection/regeneration/review/reference composition and owns `Validation/HouseShowcaseArchitectureValidation.unity`. `Assets/Scenes/HouseShowcase` remains integration only. Production geometry/material/texture/SDF/collision authority stays in its existing owning modules.

H1: a new architecture engine is required. H2: existing guild-house production generation is reusable if exposed behind semantic provider/config contracts. Inspection and implementation support H2. The feature adds generic contracts/registry and an adapter over existing guild generation; it does not fork geometry, materials, Storage, rendering, or collision.

## Implemented foundation

Verified production source is `1efa11232cfbfee5a91fa1ae79c5319490ca0f72`.

- `ArchitectureModel`, `ArchitectureRegistry`, `ArchitectureReviewRegistry`, and durable review metadata provide semantic profile/provider/archetype discovery, deterministic request/result identity, explicit parameter support, production capability metadata, reference descriptors, and deterministic exterior/interior poses.
- `GuildHouseArchitectureProvider` adapts existing guild composition/authoring. It supports footprint, room count, storey count, and floor height; unsupported roof/foundation/opening/trim/detail overrides are rejected explicitly.
- Existing guild authoring now honors multi-storey lodge shells, reserves stairwells, authors occupancy-backed stair flights, carves real window apertures with production Glass, and uses the existing curved primitive/rasterizer path for signed-distance canopy presentation.
- `ICurvedStructureAuthoringSession` is a narrow capability extension; `StructureAuthoringSession` implements it through the existing production curved primitive emitter/rasterizer, preserving boundary samples and write budgeting.
- Focused Structures tests cover registration/reuse, deterministic identity, four seed-driven massing variants, invalid parameter rejection, topology/circulation, real window operations, materials and curved/SDF authoring.
- Structures and Showcase each own focused production-path validation scenes/scenarios.
- `HouseShowcase` is now registry/provider-driven, exposes profile/provider/archetype/seed/supported parameters, same-input regeneration, distinct-seed generation, deterministic exterior/interior review, free-fly inspection, and durable reference/runtime pair metadata. Furnishing controls were removed from this feature path.
- Neutral reference art and `Documentation/ProceduralArchitectureFoundation.md` document the review and extension boundary.

## Blast radius and budgets

The production diff is scoped to architecture contracts/provider/tests/validation, the HouseShowcase consumer, one narrow engine curved-authoring capability, neutral reference/documentation, and the assigned SceneIssue. No Kentridge runtime source is modified.

Production authoring retains bounded budgets: HouseShowcase uses an 8,000,000-write structure session; the four-variant module validation uses 20,000,000 writes and fails on budget exhaustion; Storage/render lifecycle is explicitly cleared/disposed on rebuild/disable. Registries reject duplicate keys rather than accumulating per-regeneration registrations. Shared APIs contain semantic keys rather than raw material IDs or scene coordinates; material IDs remain inside the production guild adapter/authoring and composition reference/camera constants remain Showcase-owned.

## Final exact-SHA validation

The first final-source request `e710fd02492b38a3f2b94287fbc65175475d2e18` / run `34169991521` correctly exposed a product compile failure: `Game.Structures.Runtime` did not reference `VoxelEngine.Storage.Api` after the curved-authoring contract exposed `VoxelSurfaceFlags`. The owning asmdef dependency was fixed in `1efa11232cfbfee5a91fa1ae79c5319490ca0f72`.

The follow-up exact targeted-CI transport `b9d7fcb542b340853cf902cebda2cbca2a4b0e23` / run `34178318979` validated parent source `1efa11232cfbfee5a91fa1ae79c5319490ca0f72` and completed successfully:

- all repository-derived required EditMode/PlayMode assemblies executed with nonzero results;
- all repository-derived module-local players completed, including Structures and Showcase architecture validation;
- canonical `KentridgePlayableSlice` integration completed successfully;
- standalone `Assets/Scenes/HouseShowcase.unity` replay built and completed at 1600x900;
- screenshot previews and durable artifact `single-test-34178318979` were emitted.

## Direct built-player review

The artifact was downloaded and inspected directly.

- `ArchitectureFoundationRuntimeValidation` shows four visibly distinct deterministic footprints/massing variants with complete walls, roofs, centered entrances, real windows, grounded foundations, production materials/textures, and an interior stair path. Runtime assertions also prove Glass occupancy, signed-distance boundary samples, two storeys, one stair flight, and walkable entrance/interior clearance.
- `HouseShowcaseArchitectureValidation` provides a complete 78-second review sequence. Logs prove same-input deterministic identity, seed variants 1/2/3 with distinct hashes and bounds, explicit three-storey/floor-height/room configuration switching, semantic `hightown` profile switching, canonical exterior/interior review poses, and final reference/runtime pair metadata. Direct capture review found no missing supported structure parts, bad support/contact, unusable openings, visible seams, or missing production presentation.
- The standalone SceneIssue replay shows the canonical exterior and interior correctly. Its final 30-second interval capture occurs immediately after the first seed rebuild and catches the renderer during republish; the corresponding longer HouseShowcase validation frames show the rebuilt variant correctly, so this is not a persistent geometry defect.
- Canonical Kentridge integration completed with its required runtime assertions/captures and no architecture-specific alternate enabling path or demonstrated renderer/material/collision regression.

## Closure readiness

Every implementation and acceptance checkbox is satisfied. The issue is ready for the repository-prescribed closure sequence: update fixed/resolved evidence, move only this issue from `open/` to `closed/`, then merge current `origin/master` into `fixes/agent-1`, open/update the feature PR, enable auto-merge, pass the required `affected` gate, and confirm the closed SceneIssue on `origin/master`.