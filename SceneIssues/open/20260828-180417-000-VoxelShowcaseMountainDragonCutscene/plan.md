# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain, readable winding ascent, normal grounded traversal to the summit, visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Closure requires a source-matched startup bake, green exact focused acceptance, complete production `AutoWalk -> CharacterMotor.Step` traversal, and human-reviewed approach/base/middle/upper/summit/dialogue captures.

## Proven state
WorldBuilder/shared modules own the mountain, path, placeholder, and encounter. The footprint is west of castle-owned feature suppression; alternating ramps have explicit clear landings; the 24-voxel headroom contract is covered. Output-equivalent Box/Frustum raster fast paths and CI-only successful-bake shutdown keep the baseline source bake near the 240 s guard. Current master `e95324aeaef6...` remains an ancestor of the feature branch (`behind_by=0`).

Run `33298125653` on source `2106820d31cb...` completed a fresh bake in 225 s at ~5.37 GB RSS/no swap growth. The real player completed all 17 grounded/Y-checked waypoints in 57.4 s; named captures show the mountain/path, supported red summit placeholder, and `Hello, I'm Mr. Dragon.` Its only focused failure was missing startup-bake dragon material at `(-1112,530,200)`: the fixed-altitude placeholder crosses Y=512 but terrain-surface baking omitted its upper region while runtime streaming later generated it.

## Latest discriminator / selected fix
Bake-only explicit `Structure + FixedAltitude` coverage plans the placeholder's two vertical layers without broad sky residency. Run `33299149060` exposed only unsupported `int3` arithmetic; component-wise footprint math repaired that compile defect.

Exact request `b68865a45a70...` / run `33300355968` then compiled and generated the corrected startup image: the artifact logs `200 regions, 18.2 MiB`, castle voxels `41,129,996`, content signature `0x7C8A5152`, and successful asset import. The workflow killed the bake step at ~241 s during process shutdown, so the requested test was skipped; built-player capture still succeeded. This proves the additional upper region is correct but the normal terrain + full-landform generation path consumes the previous ~15 s margin.

The selected cost fix is now implemented generically but bake-only in use: `FeatureRegionBuildScope` defaults to `All` for every existing runtime caller, while `FixedAltitudeStructures` can be selected only by offline composition that already proved skipped definitions output-neutral. `ShowcaseWorld` uses that scope only for a planned explicit fixed-structure region that is still absent after the normal startup surface pass; any generated/resident region keeps the normal full-generation path.

Focused regression `ShowcaseBakeExplicitStructureCoverageTests.SparseUpperDragonStructureBuildMatchesFullCatalogueSemanticOutput` builds the actual mountain catalogue's upper dragon region from canonical-empty storage with both full and scoped builders, requiring identical semantic hash and serialized bytes, exactly one scoped structure instance, and exactly one resident region. The planner regression still requires exactly two dragon-owned vertical layers. Both regressions are now invoked by the exact final acceptance method, so the single allowed request validates the optimization directly rather than merely compiling it.

## Remaining gates
1. Re-check latest source diff/master ancestry and issue the next exact-parent request only through `ci-test/fixes/agent-4`; leave it untouched while queued/running.
2. Require fresh bake <240 s/<14 GB, Unity reopen, green `MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay`, complete built-player replay/captures, and accepted payload/source provenance.
3. Record measured cost/runtime/provenance in durable assignment metadata and retrieve/commit the accepted generated payload + manifest if the existing workflow artifact permits it without another CI transport.
4. Only after every checklist/acceptance item is green, complete metadata, move only this assignment `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge then-current master, and push exact head to `origin/master` non-force.
