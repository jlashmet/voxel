# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain, readable winding ascent, normal grounded traversal to the summit, visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Closure requires a source-matched startup bake, green exact focused acceptance, complete production `AutoWalk -> CharacterMotor.Step` traversal, human-reviewed approach/base/middle/upper/summit/dialogue captures, and the accepted startup image checked into the branch used by the scene.

## Proven implementation state
WorldBuilder/shared modules own the mountain, path, placeholder, and encounter. The footprint is west of castle-owned feature suppression; alternating ramps have explicit clear landings; the 24-voxel headroom contract is covered. Output-equivalent Box/Frustum raster fast paths plus bake-only fixed-altitude structure coverage preserve normal runtime semantics while keeping the source bake inside the fixed CI budget.

The exact successful-bake completion predicate rejects interactive editor bakes, local/non-CI batches, `GITHUB_ACTIONS=false`, `-runTests`, other execute methods, and generic batch processes. After run `33310154810` proved managed `Environment.Exit(0)` left the native Unity PID alive until the watchdog, commit `aa3dab95a2e859d63ab63b0ad3a51ab3e51380e2` changed only the exact successful macOS/Linux CI bake callback to libc `_exit(0)` (managed fallback elsewhere).

## Final exact-SHA gate — green
Current-master merge source `2100df40287a9ec9d66407e54ab92b9cc2e58b25` was requested through transport commit `0b1cac9068bbed415d4ad5054b82e268f51707ac`. Run `33310677691` / job `99255036891` is green end-to-end: fresh bake, native process exit, Unity reopen, exact PlayMode acceptance, real-player capture, artifact upload, and final commit status all succeeded.

The final bake stayed inside the unchanged 240 s / 14 GiB watchdog contract and produced `200 regions, 18.2 MiB`, seed `0x5EED1234`, `41,129,996` castle voxels, signature `0x7C8A5152`. The accepted artifact payload is `19,122,896` bytes with SHA-256 `cf51ac9fd9da8a753bf625cc14430ee4941e1ea2f7404be43fd3a2f8323c5da4`. The exact NUnit case passed in `17.047203 s`. Built-player production movement reached all 17 waypoints in `57.5 s`, grounded at summit proximity; exact-run named captures pass human visual review and show the required dialogue.

## Sole remaining gate — checked-in startup image
The branch still tracks the pre-feature `ShowcaseWorld.bytes` at `11,074,525` bytes (Git blob `53b8673358e6166445162d64be4f4af89c132a50`) and has no tracked `ShowcaseWorld.manifest.txt`. The production provenance contract requires a manifest whose SHA matches the loaded bytes, so the accepted generated artifact is not yet the startup image a clean checkout ships.

Do not close the feature until the accepted `19,122,896`-byte payload and its manifest are committed through a repository-sanctioned binary refresh path. Artifact retrieval succeeded without another CI transport, but the currently connected GitHub write API exposes text/Git-data writes and cannot stream the retrieved 19 MiB binary file into a repository blob. Do not work around this by weakening `ShowcaseStartupBakeContract`, runtime-authoring the mountain, creating a new CI transport, or accepting the stale payload.

## Remaining sequence
1. Replace the tracked stale `ShowcaseWorld.bytes` with the accepted final artifact and add the matching manifest, preserving exact SHA/signature provenance.
2. Validate that clean-checkout startup consumes those exact checked-in bytes; because this changes shipped binary state, require an exact-source validation before closure if repository policy treats the payload commit as a new exact SHA gate.
3. Only after every task and issue acceptance criterion is complete, write pending metadata, move only this assignment `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge then-current `origin/master`, and push the exact feature head to `origin/master` non-force.
