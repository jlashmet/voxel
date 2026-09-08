# HouseShowcase Procedural Architecture Foundation Tasks

## 1. Inspect the existing production seams
- [x] Fetch current `origin/master` and review `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` before implementation.
- [x] Inspect `Assets/Scenes/HouseShowcase.unity`, its player scenario, and the `HouseShowcase` runtime/controller.
- [x] Inventory the current house configuration controls, seeds, dimensions, roof/gable choices, openings, floors/storeys, and any other existing generation parameters.
- [x] Identify the authoritative production generator(s) and configuration types HouseShowcase currently consumes.
- [x] Identify the production voxel/material/texture/SDF/collision/traversal paths used by generated structures.
- [x] Identify every affected module root and its owned test/validation surface; add missing module-local validation tasks before changing player-visible behavior.
- [x] Record the results of the first hypotheses/discriminators in `plan.md` and choose the narrowest extension point.

## 2. Establish the shared semantic registration contract
- [x] Add or extend a semantic registration mechanism for a town/style profile without encoding scene coordinates or magic IDs.
- [x] Add or extend a semantic structure archetype/provider registration mechanism.
- [x] Support deterministic seed selection through the shared contract.
- [x] Expose explicit supported generation parameters rather than arbitrary scene-side mutation.
- [x] Preserve existing useful house parameters and add only parameters demonstrated necessary by production generation/review.
- [x] Ensure future residence, shop, inn, church, warehouse, civic, castle/landmark, and similar providers can participate without forcing all structures through one inappropriate geometry implementation.
- [x] Prove registration can be extended by an independent provider/fixture without editing a hard-coded HouseShowcase selection switch.

## 3. Deterministic production generation
- [x] Select one existing production procedural structure archetype as the foundation proving fixture.
- [x] Define one canonical review profile/archetype/seed/parameter configuration.
- [x] Prove identical profile + archetype + seed + parameters reproduces identical authoritative voxel output.
- [x] Add at least three additional seeds for the same archetype.
- [x] Verify all four seeds produce meaningful architectural variation rather than cosmetic-only noise.
- [x] Verify all four variants remain structurally valid, correctly scaled, and production-compatible.
- [x] Add focused behavioral regression coverage for same-input determinism and seed variation.

## 4. Production voxel/material/texture/SDF realization
- [x] Ensure showcased structures are realized from the production authoritative voxel data path.
- [x] Verify material assignments are carried by the production structure/voxel representation rather than scene-local preview policy.
- [x] Verify source textures resolve through the normal production texture/material registration path.
- [x] Verify textures are visibly applied to the rendered structure, with production scale/orientation behavior.
- [x] Verify SDF/presentation data can be supplied for curved surfaces while collision/world truth remains discrete occupancy.
- [x] Add focused behavioral tests for material/texture/SDF invariants that can be asserted without substituting a fake renderer.
- [x] Do not add arbitrary imported static meshes or a parallel material/texture/SDF authority.

## 5. Complete architectural structure contract
- [x] Verify/configure foundations through production generation.
- [x] Verify/configure exterior walls and roof volumes.
- [x] Verify/configure doors and door openings.
- [x] Verify/configure windows and window openings.
- [x] Verify/configure floors and multi-storey spacing where applicable.
- [x] Verify/configure stairs between usable storeys where applicable.
- [x] Verify correct world scale and believable room/ceiling dimensions.
- [x] Verify collision derives from the same structure occupancy.
- [x] Verify exterior-to-interior entry is usable through normal traversal.
- [x] Verify the representative interior is physically walkable and circulation is not blocked by generated structure geometry.
- [x] Keep furniture and decorative prop dressing out of scope.

## 6. HouseShowcase review UX
- [x] Update the existing HouseShowcase rather than creating a competing top-level architecture showcase.
- [x] Add reviewer selection for semantic town/style profile.
- [x] Add reviewer selection for structure archetype/provider.
- [x] Add reviewer control for deterministic seed.
- [x] Surface supported structure parameters in a semantic/config-driven way.
- [x] Add deterministic regeneration after seed/parameter changes.
- [x] Provide deterministic exterior inspection/review poses.
- [x] Provide an interior/player inspection mode that can verify entrance, openings, floors, stairs, storeys, ceiling heights, and circulation.
- [x] Ensure HouseShowcase does not become authoritative for generation, materials, textures, SDFs, or collision.

## 7. Reference-comparison foundation
- [x] Add a data-driven contract associating a canonical structure configuration with one or more concept/reference images.
- [x] Add deterministic review camera-pose metadata for each associated reference view.
- [x] Preserve reference identifier/path, profile, archetype/provider, seed, parameters, and camera pose in review evidence metadata.
- [x] Provide a reviewer-friendly paired or side-by-side reference/runtime comparison result.
- [x] Do not use raw pixel similarity as the art-quality acceptance oracle.
- [x] Ensure later town issues can add references/configurations without editing generic comparison machinery.

## 8. Module-local validation ownership
- [x] For each affected player-visible/runtime module, create or update the focused `<Module>/Validation/` scene that exercises the real production path.
- [x] Add/update module-local `*.player-scenario.json` files where actions, captures, timing, or runtime assertions are required.
- [x] Keep module-local validation minimal/deterministic and free of test-only geometry, fake materials, or a parallel renderer.
- [x] Validate production generation/configuration in its owning module independent of HouseShowcase.
- [x] Validate production material/texture/SDF realization in the owning module(s) independent of HouseShowcase where applicable.
- [x] Validate traversal/interior behavior through the owning production path where applicable.

## 9. HouseShowcase integration evidence
- [x] Update `HouseShowcase.player-scenario.json` to exercise deterministic regeneration.
- [x] Exercise at least two seed/configuration changes in the built player.
- [x] Capture the canonical exterior review pose.
- [x] Capture representative additional-seed exterior evidence.
- [x] Exercise normal interior/player inspection and capture a usable interior view.
- [x] Exercise the reference-comparison path and produce durable paired/reference-runtime evidence.
- [x] Inspect the durable built-player screenshots directly; structural tests alone do not satisfy visual acceptance.

## 10. Reuse documentation and quality review
- [x] Add a concise extension note explaining how a future town SceneIssue supplies a style/profile, provider/archetype, seed/parameters, materials/textures/SDF data, and references/review poses.
- [x] Document which policy belongs in generic production modules versus town composition versus HouseShowcase review configuration.
- [x] Review for scene-specific branching, magic IDs, incidental ordering/index contracts, or duplicated authorities and remove them.
- [x] Measure relevant generation/runtime cost or allocation impact against repository budgets.
- [x] Review the final rendered proving fixture at the repository production-quality bar for scale, openings, grounding, material/texture application, and obvious procedural defects.

## 11. Validation and closure
- [x] Run the smallest focused behavioral regressions proving determinism, seed variation, reuse boundaries, and required presentation invariants.
- [x] Run repository-derived module-local built-player validation for every affected runtime module.
- [x] Run the HouseShowcase built-player integration scenario and preserve durable evidence.
- [x] Confirm no final Mounting Force town-specific art, furniture/prop dressing, or final town/world placement was introduced.
- [x] Review the final diff and ensure every acceptance criterion in `issue.json` is satisfied.
- [x] Complete `resolutionSummary`, `regressionTest`, and `fixCommit` after verified results exist.
- [x] Use exact-SHA targeted CI on `ci-test/fixes/agent-1` without replacing queued/running work.
- [x] After all gates pass, move only this SceneIssue from `open/` to `closed/`, set `status=fixed` and `resolvedUtc`. Current-master merge, final PR, auto-merge, and the PR `affected` gate are the post-closure promotion steps prescribed by `SceneIssues/README.md` and are executed after this bookkeeping commit rather than pre-claimed in this checklist.

## Acceptance checklist
- [x] HouseShowcase is a semantic/config-driven production integration consumer, not an authority.
- [x] Future town/style profiles and structure providers can register without scene-specific hard-coded selection logic.
- [x] Reviewer controls cover profile, archetype/provider, seed, supported parameters, deterministic regeneration, exterior inspection, and interior/player inspection.
- [x] Same input is deterministic and at least four seeds of one production archetype prove meaningful variation.
- [x] Production materials, textures, and SDF-based curved presentation work end-to-end on production voxels.
- [x] Doors, windows, floors, stairs, foundations, scale, collision, and traversal remain production-compatible; the representative interior is walkable.
- [x] A data-driven reference-image + deterministic review-pose contract produces durable reference/runtime comparison evidence.
- [x] Required module-local built-player validation and HouseShowcase integration evidence are green and visually inspected.
- [x] Extension documentation is sufficient for later per-town Mounting Force architecture SceneIssues.
- [x] No final town-specific art, furniture/prop dressing, or final world placement is included in this foundation.

## Verified evidence

- Exact production source `1efa11232cfbfee5a91fa1ae79c5319490ca0f72`; targeted-CI transport `b9d7fcb542b340853cf902cebda2cbca2a4b0e23`; workflow run `34178318979` completed successfully.
- The first final-source run `34169991521` exposed the missing `VoxelEngine.Storage.Api` assembly dependency; that product failure was fixed before the successful exact-SHA run.
- `ArchitectureRegistryTests.RegistrationBoundaryAcceptsIndependentProviderWithoutEditingShowcaseSelectionLogic` registers an independent Shop provider and proves generic provider discovery without a HouseShowcase switch edit.
- Same-request determinism is proven by stable request and realization hashes. `ArchitectureIdentityHasher.HashPrototype` covers shell kind/style, origin, footprint, floor height/count, and every room role/floor/cell/bounds; production authoring consumes that deterministic prototype without a second random source.
- Four built-player variants produce four distinct realization hashes and four distinct footprints while retaining valid openings, floors, stairs, storeys, scale, and interior shells.
- Production-path validation asserts real Glass occupancy, signed-distance boundary samples, `CollisionFromOccupancy`, production material textures, one stair flight for the two-storey proving fixture, and a player-sized clear entrance/interior volume over authoritative voxel floors.
- The four-variant Structures validation measured `1,973,140` authored voxel writes against its `20,000,000` write budget. HouseShowcase uses an `8,000,000` write session and explicitly clears/disposes Storage/render state on rebuild/disable; no budget weakening or unbounded registration accumulation was introduced.
- The 78-second HouseShowcase built-player evidence proves canonical exterior/interior review, deterministic replay, three additional seeds, 3-storey parameter switching, semantic profile switching, and durable reference/runtime pair metadata. Every required image was directly reviewed for structure completeness, scale, grounding, openings, production material/texture presentation, interior usability, and seams.
- Canonical `KentridgePlayableSlice` integration completed successfully in the same repository-derived validation plan, with no demonstrated renderer/streaming/material/collision regression or parallel architecture enabling path.