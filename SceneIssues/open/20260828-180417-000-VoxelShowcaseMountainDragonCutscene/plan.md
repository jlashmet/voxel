# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain, readable winding ascent, normal grounded traversal to the summit, visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Closure requires a source-matched startup bake, green exact focused acceptance, complete production `AutoWalk -> CharacterMotor.Step` traversal, and human-reviewed approach/base/middle/upper/summit/dialogue captures.

## Proven state
WorldBuilder/shared modules own the mountain, path, placeholder, and encounter. The footprint is west of castle-owned feature suppression; alternating ramps have explicit clear landings; the 24-voxel headroom contract is covered. Output-equivalent Box/Frustum raster fast paths and CI-only successful-bake shutdown keep the baseline source bake near the 240 s guard. Current master `e95324aeaef6...` is an ancestor of the feature branch.

Run `33298125653` on source `2106820d31cb...` completed a fresh bake in 225 s at ~5.37 GB RSS/no swap growth. The real player completed all 17 grounded/Y-checked waypoints in 57.4 s; named captures show the mountain/path, supported red summit placeholder, and `Hello, I'm Mr. Dragon.` Its only focused failure was missing startup-bake dragon material at `(-1112,530,200)`: the fixed-altitude placeholder crosses Y=512 but terrain-surface baking omitted its upper region while runtime streaming later generated it.

## Latest discriminator / selected fix
Bake-only explicit `Structure + FixedAltitude` coverage now plans the placeholder's two vertical layers without broad sky residency. Run `33299149060` exposed only unsupported `int3` arithmetic; component-wise footprint math and signed floor-division regression repair that compile defect.

Exact request `b68865a45a70...` / run `33300355968` then compiled and generated the corrected startup image: the artifact logs `200 regions, 18.2 MiB`, castle voxels `41,129,996`, content signature `0x7C8A5152`, and successful asset import. The workflow killed the bake step at ~241 s during process shutdown, so the requested test was skipped; built-player capture still succeeded. This proves correctness generation is reached but the one extra upper region consumes the prior ~15 s margin.

Next discriminator: inspect `GenerateRegionBlocking`/generic feature construction for the upper sparse layer. Prefer a bake-only generic feature-materialisation path that preserves normal runtime generation and exact fixed-structure semantics while avoiding unnecessary terrain/landform work in an otherwise empty sky region.

## Remaining gates
1. Add the smallest generic bake-only cost fix and focused behavioral regression; preserve 240 s/14 GB contracts and re-check blast radius/master ancestry.
2. Reuse only `ci-test/fixes/agent-4` for a fresh exact-parent final PlayMode request; leave it untouched while queued/running.
3. Require bake <240 s/<14 GB, Unity reopen, green final acceptance, complete built-player replay/captures, and accepted payload/source provenance.
4. Only after every checklist/acceptance item is green, complete metadata, move only this assignment `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge then-current master, and push exact head to `origin/master` non-force.
