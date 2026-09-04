# Experiment 023 — Gallery content-dirty remesh

## Trigger

Exact run `33839912405` on feature source `ca8b634ab5b381483cdea0e739e7a19fa4f43ca8` proved that `_storage.PublishAllResidentRegions()` advances the change feed but does not repair the mandatory Gallery authored-breakable image. The full-resolution frame still showed the pre-authoring world underside/void.

## Discriminator

`VoxelChangeKind` distinguishes content changes (`Occupancy`, `BaseMaterial`, `SurfaceStyle`, `Coating`, `Water`) from `Residency`.

The Gallery's production structure authoring session receives `ReadStorage` and `MutationStorage`; it does not receive the world change journal. This is appropriate while initial Gallery content is built before presentation consumes it, but SecretDiscovery is composed after a baked Gallery is already resident/renderable.

Ordinary world edits explicitly call `MarkDirty(voxel)` after successful mutation. More importantly, the existing completed-castle path has an established bounded bulk-mutation contract: after castle authoring finishes it comments that every touched region "has to be re-meshed and re-uploaded" and publishes each touched region with `VoxelChangeKind.All`.

Therefore a residency-only publication is the wrong semantic for post-bake SecretDiscovery. Already resident chunks need content-dirty invalidation.

## Regression

`WorldbuildingGallerySecretDiscoveryPublicationTests.PostBakeSecretAuthoringPublishesContentDirtyRegions` now:
- restores the production Gallery bake with the required startup radius 4;
- preloads Gallery and all secret-cave regions before sampling the change cursor;
- composes SecretDiscovery;
- reads only post-cursor records; and
- requires at least one content-dirty kind (`Occupancy | BaseMaterial | SurfaceStyle | Coating`).

This deliberately rejects a feed containing only `Residency`, so the prior `_storage.PublishAllResidentRegions()` implementation cannot satisfy the regression merely by advancing `CurrentVersion`.

## Selected production fix

`EnsureWorldbuildingGallerySecretDiscoveryBlocking()` now publishes the same bounded 3x3 region footprint that it preloads for cave/pocket/clue authoring using `_changes.PublishRegion(region, VoxelChangeKind.All)` after all writes succeed and before readiness is exposed.

This reuses the repository's established post-bulk-authoring remesh contract, stays bounded to nine regions, and avoids renderer-specific coupling or another camera workaround.

## Expected proof

Next exact-SHA validation must show:
1. Showcase focused EditMode passes the content-dirty regression.
2. Automatic CaveWorldBuilder, Showcase, WorldBuilder, and Kentridge validation all pass.
3. Exact Gallery SceneIssue replay still passes semantic clue counts.
4. Full-resolution `02-authored-breakable-boundary.png` contains authored cave/false-wall geometry at gameplay scale rather than underside/void.

If item 4 still fails, this hypothesis is falsified and no further notification variant should be attempted without inspecting the renderer consumer or synchronization state.
