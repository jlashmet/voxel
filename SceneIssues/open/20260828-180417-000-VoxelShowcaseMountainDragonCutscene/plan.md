# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain, readable winding ascent, normal grounded traversal to the summit, visibly supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Closure requires a source-matched startup bake, green exact focused acceptance, complete production `AutoWalk -> CharacterMotor.Step` traversal, and human-reviewed approach/base/middle/upper/summit/dialogue captures.

## Proven state
WorldBuilder/shared modules own the mountain, path, placeholder, and encounter. The footprint is west of castle-owned feature suppression; alternating ramps have explicit clear landings; the 24-voxel headroom contract is covered. Output-equivalent Box/Frustum raster fast paths keep the source bake near the 240 s guard. Current master `e95324aeaef6...` remains an ancestor of the feature branch (`behind_by=0`).

Run `33298125653` on source `2106820d31cb...` completed a fresh bake in 225 s at ~5.37 GB RSS/no swap growth. The real player completed all 17 grounded/Y-checked waypoints in 57.4 s; named captures show the mountain/path, supported red summit placeholder, and `Hello, I'm Mr. Dragon.` Its only focused failure was missing startup-bake dragon material at `(-1112,530,200)` because the fixed-altitude placeholder crosses Y=512 while terrain-surface baking originally omitted its upper region.

Bake-only explicit `Structure + FixedAltitude` coverage now plans the placeholder's two vertical layers without broad sky residency. The sparse structure build is output-equivalent to the full catalogue build by semantic hash and serialized bytes, and the exact acceptance invokes both planner and scoped-build regressions. Run `33302337781` proved that repaired source bake persisted/imported `200 regions, 18.2 MiB` before timing out only during Unity teardown.

## Latest discriminator / selected fix
Exact feature SHA `3635cc2ee657...` changed only the already-gated CI completion action from `EditorApplication.Exit(0)` to `Environment.Exit(0)`. Exact request `90e824415962...` / run `33310154810` was left untouched through completion. Its fresh bake again reached the 240 s watchdog boundary and the requested test was skipped, while built-player capture still succeeded.

The new artifact is more discriminating than the earlier graceful-exit run: `showcase-bake.log` ends immediately after the successful `200 regions, 18.2 MiB` bake/import log. It contains no `Application is exiting`, domain-unload, cleanup, or later Unity output. Therefore managed `Environment.Exit(0)` stopped managed logging but did not make the native Unity PID disappear; `tools/unity-run.sh` continued polling that PID until its exact 240 s watchdog killed the session.

The next change remains editor/CI-only but must cross the native process boundary. Preserve the strict existing `GITHUB_ACTIONS=true` + batch + exact showcase `-executeMethod` policy and all write/import/save/success-log ordering, then use a POSIX process-level `_exit(0)` on the macOS/Linux CI editor path so the native Unity process exits immediately with status 0. Retain `Environment.Exit` only as a non-POSIX fallback. The injectable completion policy remains the regression seam; interactive editor bakes, local/general batch invocations, `-runTests`, other execute methods, runtime streaming, voxel composition, and the 240 s/14 GB workflow contracts remain unchanged.

## Remaining gates
1. Implement the native exact-CI post-success exit and record the failed `Environment.Exit` discriminator without changing general editor/runtime behavior.
2. Re-check source diff/master ancestry, then issue the next exact-parent request only through `ci-test/fixes/agent-4`; leave it untouched while queued/running.
3. Require fresh bake <240 s/<14 GB with process exit success, Unity reopen, green `MountainDragonFinalAcceptanceTests.NaturalizedMountainBakeAndEncounterAreReadyForBuiltPlayerReplay`, complete built-player replay/captures, and accepted payload/source provenance.
4. Record measured cost/runtime/provenance in durable assignment metadata and retrieve/commit the accepted generated payload + manifest if the existing workflow artifact permits it without another CI transport.
5. Only after every checklist/acceptance item is green, complete metadata, move only this assignment `open -> pending -> closed`, set `status=fixed`/`resolvedUtc`, merge then-current master, and push exact head to `origin/master` non-force.
