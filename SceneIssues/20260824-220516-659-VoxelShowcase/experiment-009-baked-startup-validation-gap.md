# Experiment 009 — baked startup validation gap

## Why this experiment exists

Three production geometry attempts and a post-limit stage-isolation experiment all changed the canonical Kentridge catalogue or removed a canonical stage, yet the exact saved-camera replay remained world-pixel identical. Only the FPS/debug text changed.

The post-limit `BuildPlotSurfaces` isolation was especially diagnostic: Actions run `32839747298` removed `KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(...)` from `KentridgeCombinedVoxelCatalogueCanonical.Core.cs` **in the CI checkout only**, successfully built the real standalone player, verified the frozen SceneIssue pose, and produced artifact `9560093798`. The rendered world was still unchanged.

That result falsifies the assumption that the exact VoxelShowcase replay is rasterising the current canonical catalogue into its startup neighborhood.

## Runtime path traced

The scene's serialized MonoBehaviour GUID `12be027be786465c9a6c8be1321251fd` resolves to `Assets/Scenes/Showcase/VoxelShowcase.cs`.

`VoxelShowcase` serializes:

- `m_Features = ShowcaseFeatureContent.Full`
- `m_Startup = ShowcaseStartupSource.Bake`

and passes both values into `ShowcaseWorld`.

`ShowcaseWorld.GenerateCastleOriginBlocking()` handles `Generate` specially, but on the default `Bake` path it calls `LoadBake(LoadBakeResource(...))`. The method explicitly refuses to fall back to procedural generation once the baked startup path is selected.

`ShowcaseCatalogue.Build(...)` does use `KentridgeCombinedVoxelCatalogue.Build(...)`, so there is not a second Kentridge planner hidden behind the showcase. The mismatch happens later: the default startup path restores stored voxel snapshots before runtime generation can rasterise the current catalogue into those resident regions.

The standalone capture build is also not generating a replacement world image. `tools/showcase-player-capture.sh` invokes `VoxelEngine.Showcase.Editor.ShowcasePlayerBuild.Build`, whose implementation simply calls Unity `BuildPipeline.BuildPlayer` for the requested scene. The committed `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` is therefore what the player receives unless a separate bake step is run first.

## Bake freshness gap

`ShowcaseWorldBakeCodec` currently versions the **binary envelope**, not the authored content. Version 3 contains:

- seed,
- startup radius,
- castle/feature counters,
- reference/gallery metadata,
- region coordinates, semantic hashes and compressed semantic snapshots.

It contains no Kentridge/catalogue/world-generation content revision or fingerprint. Version 2 is intentionally still accepted as schema-compatible.

Therefore an old startup image can deserialize successfully even when the source that would generate the town has changed substantially.

Repository history supports that this is happening here: the current `ShowcaseWorld.bytes` blob was last refreshed by commit `c9fac2bbb4a049e8260c33a88cda23da8a8e39c9` on 2026-08-22 (`Refresh VoxelShowcase bake for broad terrain relief`). The SceneIssue and the Kentridge catalogue work being validated are from 2026-08-24/25.

## Current falsifiable check

One-shot workflow commit `bc5563a416c938d706755ebe1ca360f2ee8c618b` runs this sequence without committing generated world bytes:

1. hash the committed `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes`;
2. run `VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld` against the current checkout;
3. hash the newly generated bake and fail if it is byte-identical;
4. build the normal standalone VoxelShowcase player from that fresh bake;
5. replay the exact saved SceneIssue camera and verify the frozen pose;
6. capture fresh-bake visual evidence.

Actions run: `32841171118`.

At the time this note was written, the full eight-region-radius rebake was still running.

## Interpretation / next decision

If the fresh bake hash changes **and** the exact world image changes, the stale startup image is proven to have invalidated all prior visual geometry checks. Subsequent scene work must validate against freshly generated/baked content, and the repository needs a build/test invariant that prevents shipping a bake whose authored-content identity is stale.

If the fresh bake changes but the exact world image is still identical, the bake itself is not the remaining explanation and diagnosis must move below the catalogue-to-storage rasterisation boundary.

No new production geometry attempt is permitted until this check resolves. The SceneIssue remains `open`.
