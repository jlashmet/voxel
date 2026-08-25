# Experiment 009 — baked startup validation gap

## Why this experiment exists

Three production geometry attempts and a post-limit stage-isolation experiment all changed the canonical Kentridge catalogue or removed a canonical stage, yet the exact saved-camera replay remained world-pixel identical. Only the FPS/debug text changed.

The post-limit `BuildPlotSurfaces` isolation was especially diagnostic: Actions run `32839747298` removed `KentridgeVerticalPlacementAdapter.BuildPlotSurfaces(...)` from `KentridgeCombinedVoxelCatalogueCanonical.Core.cs` **in the CI checkout only**, successfully built the real standalone player, verified the frozen SceneIssue pose, and produced artifact `9560093798`. The rendered world was still unchanged.

That result falsified the assumption that the exact VoxelShowcase replay was rasterising the current canonical catalogue into its startup neighborhood.

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

Repository history supports that this happened here: the committed `ShowcaseWorld.bytes` had last been refreshed by commit `c9fac2bbb4a049e8260c33a88cda23da8a8e39c9` on 2026-08-22 (`Refresh VoxelShowcase bake for broad terrain relief`). The SceneIssue and Kentridge catalogue work being validated are from 2026-08-24/25.

## Fresh-bake causal check

One-shot workflow commit `bc5563a416c938d706755ebe1ca360f2ee8c618b` ran this sequence without committing generated world bytes:

1. hash the committed `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes`;
2. run `VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld` against the current checkout;
3. hash the newly generated bake and fail if it is byte-identical;
4. build the normal standalone VoxelShowcase player from that fresh bake;
5. replay the exact saved SceneIssue camera and verify the frozen pose;
6. capture fresh-bake visual evidence.

Actions run `32841171118` completed successfully. Artifact: `9560706809` (`scene-220516-fresh-bake`), digest `sha256:41a10efffa462b2e1689f59d8092091f0e567d88eca960c5a87b6271e0f8decb`.

The committed and freshly generated startup images are different:

- committed bake SHA-256: `080eaf82fc003cfa677f1efa933e46a9301e59141f4e343010b1ebdb05ed0e59`
- fresh bake SHA-256: `f4b347d61a3f4fc04f6c8d951c82de5f7780fd25c45c8832c3d078ba5300f4b4`

The full eight-region-radius rebake completed in 202 seconds and the normal standalone player then built and ran successfully. The frozen pose was verified and the final evidence frame was `showcase-004-t055.5s-stationary.png`.

## Pixel and visual result

The fresh-bake exact replay is materially different from the stale-bake exact replay, not just in HUD text:

- full-frame mean absolute RGB difference: `15.98`
- pixels differing by more than 20 RGB levels: `33.88%` of the full frame
- below the replay/HUD area (`y >= 180`): `43.06%` differ by more than 20
- foreground: approximately `54.08%` differ by more than 20

The old replay's three conspicuous foreground stair ribbons are gone. The current generator presents one broad central climb with separated side-access flights/terraced edges rather than overlapping staircases occupying the same foreground. The named buildings visible uphill no longer read as floating over those foreground stair forms, and no unsupported structure is apparent in the saved view.

This is the first exact-camera evidence in this issue that legitimately evaluates the current Kentridge generator. The earlier no-op visual results were evaluating an August 22 baked world image instead.

## Conclusion

The stale startup image invalidated the earlier visual geometry checks. The current generator passes the captured SceneIssue view substantially better, including the reported overlapping-stair and floating-building symptoms.

The SceneIssue is **not closed yet** because the repository still contains the stale committed `ShowcaseWorld.bytes`. Closure requires:

1. regenerate and commit the VoxelShowcase startup bake from current source;
2. build/replay the normal scene from that committed bake, without an in-job replacement;
3. verify the exact saved camera still matches the fresh-bake passing view;
4. restore temporary diagnostic workflow/test state;
5. only then set `issue.json` to `fixed`.

A separate build/tooling follow-up is also warranted: the bake format has no authored-content freshness identity, so source changes can silently leave a schema-valid but visually stale startup world.
