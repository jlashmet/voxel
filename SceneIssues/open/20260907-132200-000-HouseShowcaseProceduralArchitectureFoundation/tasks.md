# HouseShowcase Procedural Architecture Foundation Tasks

## 1. Inspect the existing production seams
- [ ] Fetch current `origin/master` and review `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` before implementation.
- [ ] Inspect `Assets/Scenes/HouseShowcase.unity`, its player scenario, and the `HouseShowcase` runtime/controller.
- [ ] Inventory the current house configuration controls, seeds, dimensions, roof/gable choices, openings, floors/storeys, and any other existing generation parameters.
- [ ] Identify the authoritative production generator(s) and configuration types HouseShowcase currently consumes.
- [ ] Identify the production voxel/material/texture/SDF/collision/traversal paths used by generated structures.
- [ ] Identify every affected module root and its owned test/validation surface; add missing module-local validation tasks before changing player-visible behavior.
- [ ] Record the results of the first hypotheses/discriminators in `plan.md` and choose the narrowest extension point.

## 2. Establish the shared semantic registration contract
- [ ] Add or extend a semantic registration mechanism for a town/style profile without encoding scene coordinates or magic IDs.
- [ ] Add or extend a semantic structure archetype/provider registration mechanism.
- [ ] Support deterministic seed selection through the shared contract.
- [ ] Expose explicit supported generation parameters rather than arbitrary scene-side mutation.
- [ ] Preserve existing useful house parameters and add only parameters demonstrated necessary by production generation/review.
- [ ] Ensure future residence, shop, inn, church, warehouse, civic, castle/landmark, and similar providers can participate without forcing all structures through one inappropriate geometry implementation.
- [ ] Prove registration can be extended by an independent provider/fixture without editing a hard-coded HouseShowcase selection switch.

## 3. Deterministic production generation
- [ ] Select one existing production procedural structure archetype as the foundation proving fixture.
- [ ] Define one canonical review profile/archetype/seed/parameter configuration.
- [ ] Prove identical profile + archetype + seed + parameters reproduces identical authoritative voxel output.
- [ ] Add at least three additional seeds for the same archetype.
- [ ] Verify all four seeds produce meaningful architectural variation rather than cosmetic-only noise.
- [ ] Verify all four variants remain structurally valid, correctly scaled, and production-compatible.
- [ ] Add focused behavioral regression coverage for same-input determinism and seed variation.

## 4. Production voxel/material/texture/SDF realization
- [ ] Ensure showcased structures are realized from the production authoritative voxel data path.
- [ ] Verify material assignments are carried by the production structure/voxel representation rather than scene-local preview policy.
- [ ] Verify source textures resolve through the normal production texture/material registration path.
- [ ] Verify textures are visibly applied to the rendered structure, with production scale/orientation behavior.
- [ ] Verify SDF/presentation data can be supplied for curved surfaces while collision/world truth remains discrete occupancy.
- [ ] Add focused behavioral tests for material/texture/SDF invariants that can be asserted without substituting a fake renderer.
- [ ] Do not add arbitrary imported static meshes or a parallel material/texture/SDF authority.

## 5. Complete architectural structure contract
- [ ] Verify/configure foundations through production generation.
- [ ] Verify/configure exterior walls and roof volumes.
- [ ] Verify/configure doors and door openings.
- [ ] Verify/configure windows and window openings.
- [ ] Verify/configure floors and multi-storey spacing where applicable.
- [ ] Verify/configure stairs between usable storeys where applicable.
- [ ] Verify correct world scale and believable room/ceiling dimensions.
- [ ] Verify collision derives from the same structure occupancy.
- [ ] Verify exterior-to-interior entry is usable through normal traversal.
- [ ] Verify the representative interior is physically walkable and circulation is not blocked by generated structure geometry.
- [ ] Keep furniture and decorative prop dressing out of scope.

## 6. HouseShowcase review UX
- [ ] Update the existing HouseShowcase rather than creating a competing top-level architecture showcase.
- [ ] Add reviewer selection for semantic town/style profile.
- [ ] Add reviewer selection for structure archetype/provider.
- [ ] Add reviewer control for deterministic seed.
- [ ] Surface supported structure parameters in a semantic/config-driven way.
- [ ] Add deterministic regeneration after seed/parameter changes.
- [ ] Provide deterministic exterior inspection/review poses.
- [ ] Provide an interior/player inspection mode that can verify entrance, openings, floors, stairs, storeys, ceiling heights, and circulation.
- [ ] Ensure HouseShowcase does not become authoritative for generation, materials, textures, SDFs, or collision.

## 7. Reference-comparison foundation
- [ ] Add a data-driven contract associating a canonical structure configuration with one or more concept/reference images.
- [ ] Add deterministic review camera-pose metadata for each associated reference view.
- [ ] Preserve reference identifier/path, profile, archetype/provider, seed, parameters, and camera pose in review evidence metadata.
- [ ] Provide a reviewer-friendly paired or side-by-side reference/runtime comparison result.
- [ ] Do not use raw pixel similarity as the art-quality acceptance oracle.
- [ ] Ensure later town issues can add references/configurations without editing generic comparison machinery.

## 8. Module-local validation ownership
- [ ] For each affected player-visible/runtime module, create or update the focused `<Module>/Validation/` scene that exercises the real production path.
- [ ] Add/update module-local `*.player-scenario.json` files where actions, captures, timing, or runtime assertions are required.
- [ ] Keep module-local validation minimal/deterministic and free of test-only geometry, fake materials, or a parallel renderer.
- [ ] Validate production generation/configuration in its owning module independent of HouseShowcase.
- [ ] Validate production material/texture/SDF realization in the owning module(s) independent of HouseShowcase where applicable.
- [ ] Validate traversal/interior behavior through the owning production path where applicable.

## 9. HouseShowcase integration evidence
- [ ] Update `HouseShowcase.player-scenario.json` to exercise deterministic regeneration.
- [ ] Exercise at least two seed/configuration changes in the built player.
- [ ] Capture the canonical exterior review pose.
- [ ] Capture representative additional-seed exterior evidence.
- [ ] Exercise normal interior/player inspection and capture a usable interior view.
- [ ] Exercise the reference-comparison path and produce durable paired/reference-runtime evidence.
- [ ] Inspect the durable built-player screenshots directly; structural tests alone do not satisfy visual acceptance.

## 10. Reuse documentation and quality review
- [ ] Add a concise extension note explaining how a future town SceneIssue supplies a style/profile, provider/archetype, seed/parameters, materials/textures/SDF data, and references/review poses.
- [ ] Document which policy belongs in generic production modules versus town composition versus HouseShowcase review configuration.
- [ ] Review for scene-specific branching, magic IDs, incidental ordering/index contracts, or duplicated authorities and remove them.
- [ ] Measure relevant generation/runtime cost or allocation impact against repository budgets.
- [ ] Review the final rendered proving fixture at the repository production-quality bar for scale, openings, grounding, material/texture application, and obvious procedural defects.

## 11. Validation and closure
- [ ] Run the smallest focused behavioral regressions proving determinism, seed variation, reuse boundaries, and required presentation invariants.
- [ ] Run repository-derived module-local built-player validation for every affected runtime module.
- [ ] Run the HouseShowcase built-player integration scenario and preserve durable evidence.
- [ ] Confirm no final Mounting Force town-specific art, furniture/prop dressing, or final town/world placement was introduced.
- [ ] Review the final diff and ensure every acceptance criterion in `issue.json` is satisfied.
- [ ] Complete `resolutionSummary`, `regressionTest`, and `fixCommit` after verified results exist.
- [ ] Use exact-SHA targeted CI on `ci-test/fixes/agent-1` without replacing queued/running work.
- [ ] After all gates pass, move only this SceneIssue from `open/` to `closed/`, set `status=fixed` and `resolvedUtc`, merge current master, open/update the final PR, enable auto-merge, and monitor the required PR gate until merged.

## Acceptance checklist
- [ ] HouseShowcase is a semantic/config-driven production integration consumer, not an authority.
- [ ] Future town/style profiles and structure providers can register without scene-specific hard-coded selection logic.
- [ ] Reviewer controls cover profile, archetype/provider, seed, supported parameters, deterministic regeneration, exterior inspection, and interior/player inspection.
- [ ] Same input is deterministic and at least four seeds of one production archetype prove meaningful variation.
- [ ] Production materials, textures, and SDF-based curved presentation work end-to-end on production voxels.
- [ ] Doors, windows, floors, stairs, foundations, scale, collision, and traversal remain production-compatible; the representative interior is walkable.
- [ ] A data-driven reference-image + deterministic review-pose contract produces durable reference/runtime comparison evidence.
- [ ] Required module-local built-player validation and HouseShowcase integration evidence are green and visually inspected.
- [ ] Extension documentation is sufficient for later per-town Mounting Force architecture SceneIssues.
- [ ] No final town-specific art, furniture/prop dressing, or final world placement is included in this foundation.
