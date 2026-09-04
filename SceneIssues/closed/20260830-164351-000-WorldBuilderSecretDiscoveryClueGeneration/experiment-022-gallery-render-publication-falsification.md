# Experiment 022 — Gallery render publication falsification

## Exact evidence

Targeted-CI request `56af2443f352fa4ce6561c784143243ecfb0cecc` validated exact feature source `ca8b634ab5b381483cdea0e739e7a19fa4f43ca8` in workflow run `33839912405`.

Automatic module-plan discovery selected the expected CaveWorldBuilder, Showcase, and WorldBuilder module validation plus Kentridge integration. The CaveWorldBuilder focused EditMode assembly passed 3/3. Showcase reached its focused EditMode assembly and exposed one independent fixture defect: `WorldbuildingGallerySecretDiscoveryPublicationTests.EnsureSecretDiscovery_AfterPreload_PublishesResidentRegionChanges` constructs the baked Gallery world with runtime load radius 2, while the bake explicitly requires startup radius 4. `ShowcaseWorld.LoadBake` therefore rejects the fixture before secret composition executes. This failure does not discriminate the production publication behavior and should be corrected by matching the baked startup-radius contract.

The same exact run continued through the standalone SceneIssue replay successfully. Semantic acceptance logged `boundaryClueVoxels=31`, `naturalClueVoxels=30`, captured both expected frames, and reported PASS.

Full-resolution review remains a mandatory discriminator and rejects closure:
- `01-natural-cave-approach.png` renders an ordinary surface/forest/meadow view.
- `02-authored-breakable-boundary.png` still shows the world underside/sky void with floating vegetation/geometry rather than a readable authored false wall or clue.

## Competing hypotheses

### H1 — Secret geometry is absent from authoritative storage
Rejected by prior semantic composition counts and the exact physical camera-occupancy regression.

### H2 — The acceptance camera is merely inside solid terrain or badly framed
Rejected by prior materially different framing attempts and `ExactSurfaceCaveAcceptanceEyeMustOccupyAuthoredEmptyVoxel`, which proves the exact interpolated acceptance eye occupies authored empty space.

### H3 — Post-bake bulk authoring only needs `PublishAllResidentRegions()`
Falsified as a complete visual fix by exact run `33839912405`. Source `ca8b634a...` calls `_storage.PublishAllResidentRegions()` after cave/pocket/clue bulk authoring, yet the authored-breakable frame remains the pre-authoring underside/void representation. Advancing the public change feed is therefore insufficient evidence that already-rendered chunks are remeshed from the mutated storage.

### H4 — Residency publication is not a render-dirty/remesh notification for an already resident chunk
Leading hypothesis. The renderer may consume residency events only to add/remove resident chunks and may ignore them when a chunk already has a rendered representation. Normal voxel mutation/destruction may use a different dirty/change kind or explicit invalidation path.

### H5 — Correct dirty/remesh notification exists, but capture occurs before the renderer consumes it
Alternative. If the production dirty path is asynchronous and the SceneIssue waits only 1.25 seconds after a blocking storage-region ensure, a stronger completion/synchronization contract may be required. This hypothesis must not be addressed by arbitrary longer waits until the actual renderer contract is inspected.

## Next discriminator

Inspect `ShowcaseStorage.PublishAllResidentRegions()`, the concrete `IVoxelChangeFeed` event kinds, and the renderer change consumer. Compare post-bake secret authoring with a known-visible production mutation such as `ShowcaseWorld.Explode`. Add a focused regression for the renderer-relevant notification semantics at the owning production boundary before changing behavior. Do not perform another camera-only adjustment.
