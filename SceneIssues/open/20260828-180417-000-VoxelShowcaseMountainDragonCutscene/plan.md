# Plan

## Observed defect / acceptance
- Human review reopened the feature because the built VoxelShowcase did not show a convincing grounded mountain/readable ascent. Closure requires the exact built player to walk the full route normally, show the supported summit dragon and `Hello, I'm Mr. Dragon.`, and save approach/base/switchback/summit/dialogue captures that pass human visual review.
- The checked-in startup bake, not only generated source intent, must contain the accepted result.

## Competing hypotheses / discriminator
1. A stale startup bake can suppress otherwise-correct current WorldBuilder content.
2. Current authored geometry/evidence can still fail through buried/obstructed walking surfaces or through a replay harness that certifies X/Z proximity without proving vertical ascent.

Discriminator: inspect semantic bake occupancy across each path footprint, require motor feet to enter the authored vertical band at every high/landing waypoint, and review built-player frames for clipping/obstruction.

## Material results
- Stale-bake hypothesis is confirmed and guarded by bake contract revision 3 + payload SHA-256. The checked-in Aug-25 payload remains stale and the manifest payload is absent.
- Final request `fc0ae51e9d98a9f7cd61087ae8e88419daf322a4`, run `33254450374`, generated the revision-3 bake successfully but is diagnostic red. The wrapper failed an exact turn-landing midpoint probe at voxel `(-435,264,-291)` (actual air); that assertion is too brittle to prove the whole landing.
- Built-player evidence is independently red: approach/base were reached, then `switchback-0-high` and `switchback-1-low` were credited while player Y stayed near 23.85 m. The replay checks X/Z only, so it falsely certified ascent. The player then stalled around `(-45.18,23.85,-26.63)` targeting `switchback-1-high`; rendered evidence shows the camera/player embedded against gray mountain/support geometry.
- Authored slope is only 46 voxels over 360, within the production motor's 3-voxel step-up capability. The next discriminator is therefore ramp/terrain occupancy and path headroom, not simply reducing slope.
- Naturalized landform remains core + asymmetric shoulders + tapered support; expected primitive count is ~63 versus the shared 512 budget. `fixes/agent-4` already contains master `9b452aedd9b5d1b1720bf0e9184d0381f159d352`.

## Selected fix / remaining gates
- Replace exact landing midpoint proof with a footprint/column scan that proves an accessible path surface exists across every turn.
- Make the generic issue replay optionally require expected motor-foot Y/vertical tolerance at waypoints, and populate the Mountain Dragon route from authored elevations; no X/Z-only success for ascent waypoints.
- Identify and fix why the first ramp does not lift the motor—leading candidates are terrain/headroom burying the low ramp or support/landform occupancy blocking its usable surface. Add semantic occupancy checks above/below ramp samples to discriminate before changing geometry.
- The single allowed final CI transport has already been consumed and failed. Do not modify `ci-test/fixes/agent-4` again without explicit user authorization. Issue stays open until a newly authorized exact-SHA regression + built-player run is green and all captures pass human review.

## Blast radius / cost
Keep changes bounded to reusable mountain path realization, semantic acceptance, and opt-in evidence replay. Do not change normal player speed/collision or shared primitive budgets; any added path-clearance geometry must remain within the existing 512-per-instance limit and one-time bake/world-build cost envelope.
