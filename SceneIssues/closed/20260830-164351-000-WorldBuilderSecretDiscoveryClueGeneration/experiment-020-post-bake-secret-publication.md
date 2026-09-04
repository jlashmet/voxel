# Experiment 020 — post-bake SecretDiscovery publication

## Hypotheses

1. **Camera/framing defect:** the Gallery breakable-clue camera is inside or behind solid authored geometry.
2. **Missing secret geometry:** the production Gallery did not author the cave/pocket into authoritative storage.
3. **Stale derived state after post-bake bulk authoring:** authoritative storage contains the secret, but the renderer/change-feed consumers still hold the state published when the Gallery bake was restored.

## Discriminator

Prior built-player/storage evidence already falsified (1) and (2): the acceptance eye resolves to carved air in authoritative storage, and the dedicated production SecretDiscovery validation renders the authored cave. Inspecting the production mutation path found that ordinary runtime edits call `MarkDirty`, while `EnsureWorldbuildingGallerySecretDiscoveryBlocking()` performs bulk `IStructureAuthoringSession` writes after the bake is live and previously ended without change publication.

The repository already defines the intended boundary in `IVoxelStorageRuntime.PublishAllResidentRegions()`: applications call it after a bulk authoring phase. `ApplyBakedCastleSemanticRepairs()` uses that exact contract after a post-bake mutation so rendering/collision observe repaired resident voxels.

A Showcase-owned regression was added first at commits `f88fa586234d373d75101a5253e76bd279395b8f` / `bec0ff50d0efacf673d4f92cbfadef931322d0ef`. It restores the production Gallery, preloads Gallery + secret regions, samples `Changes.CurrentVersion`, then composes SecretDiscovery. Because preload publication occurs before the cursor, the regression can pass only if the post-bake secret mutation advances the change feed.

## Fix

Commit `55901ba493987c18477808d358d762504106e340` calls `_storage.PublishAllResidentRegions()` once after cave, pocket, boundary-clue, and natural-clue authoring all succeed and before `_gallerySecretDiscoveryReady` becomes true. This is startup/bounded work and reuses the existing application bulk-authoring contract; it does not add renderer-specific policy or a recurring cost.

## Verdict / next gate

Hypothesis (3) is the supported root cause. Run fresh exact-SHA targeted CI from the final feature head, require Showcase/CaveWorldBuilder/WorldBuilder module gates plus Kentridge and SceneIssue replay, then inspect full-resolution built-player evidence. Do not close if the production Gallery still fails visual acceptance.
