# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final proof must be green exact-SHA standalone-player output, `production-quality` by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `WorldRoadIntent` / resolver / `EmitTerrainCorridor`: canonical road truth.
- `ShowcaseMountainDragonLayout`: scene-only mountain/road/dragon policy.
- `CharacterMotor`: shared collision/movement; fix only reusable demonstrated defects.
- `Game.Input.Runtime`: physical input ownership; production Showcase must not require legacy Input Manager.
- `FeaturePresentationBake` -> `FarFeaturePresentationAdapter` -> `ProceduralFarFeatureRenderer`: generic far-feature presentation; preserve canonical shape/material semantics without producer-specific recipes.
- startup-bake provenance: exact source-to-payload binding.

## Proven state
Natural-landform-first composition, 280 permille grade / 42 dm cut-fill, shared road lowering, terminal route beside the cube, reusable proximity/cutscene composition, and reusable startup-bake provenance remain. Earlier experiments isolated and fixed the late CharacterMotor capsule-corner collision defect without weakening route, terrain, grade, tolerance, or summit policy.

Run `33900019648` baked/exported a matching 4,803,302-byte diagnostic candidate but is explicitly rejected as closure evidence: stale route evidence stopped replay at `upper-turn`, and exact-player captures showed giant exposed/faceted Mountain Dragon masses rather than one coherent natural mountain.

After the layered-massif redesign, exact request `66096709ef3a1e4b5ed1c44038b52b9cdae00f56` / run `33928639380` passed the requested current-source bake test in 143.158 s. Human review still rejected the production visual relationship around the mountain/path, so structural success alone remains insufficient.

Run `33947319899` selected exact feature source `1221be0f1b1bc36645ff149a27836c01802556e5` and passed `MountainDragonRoadPresentationTests.ResolvedSpiralNeverCutsDeeperThanItsOpenSkyClearance`. The resolved road centreline therefore remains within the existing 24 dm corridor clear-above envelope; grade, cut/fill, and clearance policy are not the cause of the rejected presentation.

That run exposed the production Input-System blocker. Feature work subsequently added the reusable `Game.Input.Runtime` compatibility adapter without changing Player Settings. The next exact request, run `33951141430` against feature parent `8b5883920cd3c63e713eea083688425af4c2f16a`, proves that correction in the real player: there is no game-side legacy-input exception, the character becomes grounded, and ordinary replay advances through waypoint 15/95. The run's 30 s replay budget ended before summit/cutscene acceptance, so process success is still insufficient.

## Current visual root cause and correction
Human review of run `33951141430` still rejects `01-mountain-approach.png` and `02-path-base.png`: giant flat white slab/box masses dominate the exact built-player views. Code tracing isolates this to the generic far-feature presentation path rather than authored mountain material policy:
- canonical Structures `Primitive` retains `Frustum`, `Radius`, `InnerRadius`, direction, and material;
- `FarFeaturePresentationAdapter` previously discarded taper/material when constructing `FarFeatureGeometryPrimitive`;
- `ProceduralFarFeatureRenderer` treated unsupported `Frustum` as a conservative AABB and created an unstyled white Lit material.

The required reusable correction is now implemented on the feature branch: the generic far-feature contract preserves normalized frustum taper and opaque material index, Composition transports those values from the canonical bake, and Rendering builds an actual tapered radial mass and resolves albedo from the already-installed `VoxelPresentationCatalogue`. Rendering-local regressions cover taper and authored material color; a Composition integration regression proves a canonical frustum bake arrives with shape/material/profile intact. No producer name, scene id, or game material vocabulary was added to Rendering.

## Required module-validation blocker
The same exact run's requested current-source bake test passed and produced a matching manifest, but convention-derived module validation failed only in `VoxelEngine.Structures.Tests.PlayMode.TypedStructuralSocketCompositionSceneTests.FocusedValidationDriver_ComposesFourExamples_AndRejectsRequiredIncompatibleSocket`. The test body is blocked by Unity/CoreRP `DebugUpdater.Update -> DebugManager.UpdateActions -> DebugManager.SampleAction`, which polls legacy `UnityEngine.Input` under Input-System-only Player Settings. This is separate from the now-correct game input path.

Do not enable legacy/both input and do not quarantine the required module test. The narrow CI-only correction is an Editor `InitializeOnLoad` guard keyed to the persistent CI process (`VOXEL_CI_RESULTS_ROOT` / `Voxel.CI.Persistent.Active`) that disables CoreRP runtime Rendering Debugger UI and any existing package-owned `DebugUpdater`. Normal editor/player behavior and Player Settings remain unchanged.

## Next exact-SHA gate
After durable tasks reflect these discoveries, run the exact current feature head through only `ci-test/fixes/agent-4`, requesting `VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with this SceneIssue replay and an explicit ~210 s replay budget. In that one checkout require:
1. current-source bake + matching manifest under unchanged 240 s / 14 GiB contracts;
2. repository-derived module validation for every affected module, including Structures PlayMode with no CoreRP legacy-input interference;
3. production `VoxelShowcase` log with no game-side legacy-input or other startup/runtime exception;
4. `WAYPOINT_REPLAY` setup/arm/reached/vertical/complete through all 95 waypoints, proving ordinary grounded base-to-summit movement rather than process exit alone;
5. summit collision/proximity cutscene and exact `Hello, I'm Mr. Dragon.` dialogue; and
6. fresh approach/path-base/lower-mid-upper/summit screenshots suitable for human review and free of the rejected white slab/AABB presentation.

If the far-feature or CI-guard changes fail compilation or their focused regressions fail, fix only that demonstrated reusable seam before changing mountain/road policy. If the full replay becomes valid but fresh screenshots still fail visual acceptance, diagnose the exact new built-player artifact before another geometry change; retain the open-sky, road, and CharacterMotor regressions rather than re-litigating already-falsified hypotheses.

## Remaining gates
After a valid exception-free current-source replay, human-review the exact production approach, path base, representative lower/mid/upper ascent, and summit. Require one coherent natural mountain, an open continuous carved/graded road with no trench/tunnel/causeway artifacts, supported dragon, and exact proximity dialogue. Refresh route diagnostics/evidence only from the final authoritative route. Only after visual acceptance may the candidate payload/manifest become the checked-in startup payload. Then make normal editor bake emit matching manifest, prove clean-checkout consumption, complete every checkbox and `issue.json` criterion, move only this task `open -> closed`, fetch/merge then-current master as required, revalidate the exact final feature SHA, and promote only through PR + auto-merge.
