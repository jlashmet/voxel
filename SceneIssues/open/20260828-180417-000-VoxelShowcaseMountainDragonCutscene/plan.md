# Plan

## Observed defect / acceptance
- Human review reopened the feature because the built VoxelShowcase did not show a convincing grounded mountain/readable ascent. Closure requires the exact built player to walk the full route normally, show the supported summit dragon and `Hello, I'm Mr. Dragon.`, and save approach/base/switchback/summit/dialogue captures that pass human visual review.
- The checked-in startup bake, not only generated source intent, must contain the accepted result.

## Runtime evidence and discriminator
1. Stale startup content was a real earlier failure mode; the startup-bake provenance contract and cache fingerprint already ensure CI rebuilds when WorldBuilder/Showcase source changes.
2. The last built-player replay proved a second independent defect: `switchback-0-high`/`switchback-1-low` were credited while production feet remained near Y=23.85 m, and the player then stalled against mountain/support geometry. X/Z-only arrival was a false positive.
3. `CharacterMotor.Position` is production feet position, `Grounded` is the authoritative voxel-contact state, and the motor envelope is 1.8 m high / 0.6 m wide with 0.3 m step height. `ShowcaseWorld.VoxelSize` is 0.1 m, so the accepted corridor uses 24 voxels (2.4 m) of vertical clearance.
4. `PrimitiveMode.Carve` already exists in the shared Structures API and later primitives win. No shared-engine primitive expansion or scene-local voxel hack is needed.

## Selected implementation
- The reusable mountain catalogue now emits in physical order: core/asymmetric mountain masses -> all tapered path supports -> explicit `Carve` clearance corridors -> exact authored ramps/landings/final ascent/summit path restored last.
- The landform footprint was expanded vertically to `MountainHeight + PathHeadroomVoxels + 2` so the load-bearing feature footprint contains every clearance primitive; the Showcase instance is 1200 x 306 x 1200 voxels, within the shared 1200-voxel-per-axis budget.
- Path clearance is 24 voxels (2.4 m), above the 1.8 m production motor body. The 46-voxel (4.6 m) switchback tier separation leaves the clearance envelope isolated from the next tier.
- The authored program now emits 76 primitives, below the existing Mountain Dragon regression envelope of 80 and the shared 512-primitives-per-instance ceiling. This changes one-time bake/world-build work only; no update-loop, polling, physics, or steady-state runtime cost was added.
- Semantic bake regression now requires supported occupied material below representative ramp/landing/final/summit walking columns, three separated columns across every turn landing, and 24 empty voxels above the accepted walking floor.
- Built-player replay now optionally requires grounded production-motor feet in an anchored Y band. `path-base` records the grounded feet anchor; switchbacks require +4.6 m per tier, the sixth high point requires +27.6 m, and summit waypoints require +28.0 m within 0.75 m. Matching X/Z while remaining flat or airborne cannot complete the route.

## Final CI blocker — 2026-08-29
- The one authorized final request was created on `ci-test/fixes/agent-4` at request SHA `ee738bc8511160139eb3d7ea39fbde81d8d21877`, whose parent is exact source candidate `7b5393736485e4411083bf06fd3257e42702b4bb`.
- Attempt 1 failed in the VoxelShowcase pre-test bake after the four-minute Unity execution cap while a clean self-hosted checkout was still importing/opening the project. The requested acceptance test never ran; the fallback player replay then failed because the provenance manifest had not been generated.
- The single permitted infrastructure retry re-ran the same workflow job and same exact request SHA without updating the CI ref. Attempt 2 reproduced the infrastructure failure: `Restore or bake Voxel Showcase startup world` ran from 19:49:57Z to 19:53:59Z and failed; the requested test was again skipped. Its artifact contains no `single.xml`, generated `ShowcaseWorld.bytes`, generated manifest, acceptance export, or route-named captures. The fallback player log reports the missing provenance manifest and times out at waypoint 0/15.
- No third infrastructure retry, replacement CI request, extra transport, or CI-ref update is authorized by the assignment. Keep the feature open and do not promote metadata until an externally unblocked exact-SHA gate can produce the required bake/test/replay evidence.

## Remaining gates
- Obtain an authorized infrastructure-unblocked final targeted CI result for this exact source candidate without creating an extra transport or replacing the already-used request under the current assignment constraints. Its VoxelShowcase pre-test step must generate the source-matched startup bake + manifest, the focused filter must prove semantic occupancy/headroom/encounter behavior, and the exact built player must complete the grounded vertical route.
- Retrieve and inspect the generated bake/manifest and rendered evidence from that green exact-SHA run. Commit the accepted generated startup payload/manifest to `fixes/agent-4`, then complete pending/closure metadata without changing production source.
- Human-review the exact built-player captures for grounded scale, readable continuous ascent, no buried/clipped corridor, supported summit dragon, and the exact `Hello, I'm Mr. Dragon.` dialogue.
- After all acceptance evidence is green, move only this assignment through pending -> closed, set `status=fixed`/`resolvedUtc`, merge current `origin/master`, and push the exact feature head to `origin/master` non-force, fetching/merging/retrying if master advances.
