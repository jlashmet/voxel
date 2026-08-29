# Plan

## Observed defect / acceptance
- Human review reopened the feature because the built VoxelShowcase did not show a convincing grounded mountain/readable ascent. Closure requires the exact built player to walk the full route normally, show the supported summit dragon and `Hello, I'm Mr. Dragon.`, and save approach/base/switchback/summit/dialogue captures that pass human visual review.
- The checked-in startup bake, not only generated source intent, must contain the accepted result.

## Competing hypotheses / discriminator
1. A stale startup bake can suppress otherwise-correct current WorldBuilder content.
2. Current authored geometry/evidence can still fail through buried/obstructed walking surfaces or through a replay harness that certifies X/Z proximity without proving vertical ascent.

Discriminator: inspect semantic bake occupancy across each path footprint, require the production `CharacterMotor` feet to enter the authored vertical band while grounded at every high/landing waypoint, and review built-player frames for clipping/obstruction.

## Material results
- Stale-bake hypothesis is confirmed and guarded by bake contract revision 3 + payload SHA-256. The checked-in Aug-25 payload remains stale and the manifest payload is absent.
- Prior diagnostic request `fc0ae51e9d98a9f7cd61087ae8e88419daf322a4`, run `33254450374`, generated the revision-3 bake successfully but is diagnostic red. The wrapper failed an exact turn-landing midpoint probe at voxel `(-435,264,-291)` (actual air); that assertion is too brittle to prove the whole landing.
- Built-player evidence is independently red: approach/base were reached, then `switchback-0-high` and `switchback-1-low` were credited while player Y stayed near 23.85 m. The replay checks X/Z only, so it falsely certified ascent. The player then stalled around `(-45.18,23.85,-26.63)` targeting `switchback-1-high`; rendered evidence shows the camera/player embedded against gray mountain/support geometry.
- `CharacterMotor.Position` is production feet position and `CharacterMotor.Grounded` is the authoritative voxel-contact state, so the replay can prove real traversal without camera-height approximation.
- Authored slope is only 46 voxels over 360, within the production motor's 3-voxel step-up capability. The next discriminator is therefore realized path/support occupancy and headroom, not simply reducing slope.
- Naturalized landform remains core + asymmetric shoulders + tapered support; expected primitive count is well below the shared 512 budget. The primitive API used here exposes fill/fill-if-empty rather than a repository-visible destructive clear mode, so the fix must not invent scene-local carving semantics.
- `fixes/agent-4` merged current `origin/master` `ff781ed26b1d9182fa8cd76e2d2da08abfa3765c` via two-parent merge `a3a3294e819dc578dee4af3c5f0437fba7d6d5ae` before this implementation pass.

## Selected fix / remaining gates
- Replace exact landing midpoint proof with a footprint/column scan that proves a usable path surface and player-height air band across every turn.
- Make the generic issue replay optionally require expected motor-foot Y/vertical tolerance and grounded state at waypoints. Mountain Dragon route elevations are derived from the authored `PathRise`/summit rise; no X/Z-only success for ascent waypoints.
- Keep geometry changes in the reusable mountain catalogue. If semantic occupancy proves support/landform intersects the usable corridor, bound support tops/offsets so the authored walking surface remains the topmost reachable surface; do not change the production motor or add scene-local voxel hacks.
- Add a focused regression proving occupied support below and clear headroom above representative ramp/landing footprints, plus a traversal-state regression for the vertical waypoint predicate.
- Regenerate and commit the startup bake + manifest, then prove the checked-in payload semantically contains mountain mass, switchbacks/landings, summit support, and dragon occupancy.
- The current user instruction provides fresh authorization for exactly one new final `ci-test/fixes/agent-4` request. Consume it only after source, regressions, bake artifacts, and pending metadata are ready; never replace a queued request or create another transport.

## Blast radius / cost
Keep changes bounded to reusable mountain path realization, semantic acceptance, and opt-in evidence replay. Do not change normal player speed/collision or shared primitive budgets. Any added/bounded support geometry must remain within the existing 512-primitives-per-instance limit and the one-time bake/world-build cost envelope; record final primitive count and bake/runtime evidence before closure.
