# Experiment 010 — stable near-surface coverage still does not prove physical settlements

## Exact source / transport
- Feature source: `e8b0b5351e8ac8a9ce019caa1a3ddbe82457da3f`
- CI request commit: `10b0175d25ca4c0ef2c49ec230f7e52f4ccac244`
- Workflow run: `33265086481`
- Evidence artifact: `9718439671`

## Automated result
The exact focused PlayMode production acceptance and 60-second built `KentridgePlayableSlice` harness are green. Real-player logging restores `Time.timeScale=1`, records ~3.55 m local CharacterMotor travel and ~4.68 m macro-road CharacterMotor travel, then captures every named evidence target only after four consecutive `HasCompletePublishedNearSurfaceCoverage()` passes. No startup/runtime exception prevented completion.

## Full-resolution visual result
Closure is rejected despite workflow green:
- `macro-moordell.png`: only one dark rectangular recess/blockout edge is readable; not four buildings.
- `macro-rossdam.png`: no readable settlement; a broad flat blue/cut surface dominates the frame with a black vertical cut at left.
- `macro-fairy-village.png`: roads and terrain are visible, but no readable four-building village.
- `macro-orc-village.png`: roads and terrain are visible, but no readable four-building village.
- `macro-rossdam-lake-detour.png`: the constrained road is readable, but the lake is only a thin blue band/sliver and does not cleanly prove a substantial basin/shoreline.
- `macro-southern-ridge-pass.png`: the road response is visible, but the authored ridge/pass does not read as a substantial barrier/pass silhouette.
- `macro-macro-network-overview.png`: bounded route geometry is now readable without the prior giant hole, so the tighter overview improved this one target.
- `macro-road-character-motor.png`: the traversed route surface is visible and the logged real motor traversal remains valid.

## Hypothesis discrimination
- **Four stable current-camera near-surface passes solve the prior elevated-frustum defect.** Rejected. The same predicate can remain green while target geometry is absent/unreadable.
- **The tighter overview/framing had no benefit.** Rejected; the bounded network view is materially better and readable.
- **All remaining failures are necessarily camera framing.** Not proven. The generic settlement plan has four building records, but the real-player frames still show no corresponding above-ground four-building forms at Fairy/Orc/Rossdam. Generation/composition must now be traced before another camera-only change.
- **Change production generation immediately.** Rejected until the catalogue path proves whether planned building shell/roof voxels exist above terrain and survive composition.

## Next discriminator
Trace each generic `TopDownWorldBuildingBlockoutPlan` through the production physical voxel catalogue and combined catalogue at the exact settlement coordinates. Verify above-ground shell/roof material, ground-height alignment, overwrite ordering with terrain/road primitives, and residency in the player catalogue. Correlate those voxels with run `33265086481` camera/focus positions. If the generator can report four plans while emitting no visible above-ground building, add a behavioral regression and fix the reusable catalogue. If emitted geometry is sound, keep production frozen and retarget validation from the actual emitted extents.

## Success gate
Do not promote until a new exact-SHA artifact shows four readable physical blockouts in each generic settlement, substantial clean Rossdam basin/shoreline, readable ridge/pass geography, continuous road + real motor traversal, and the connected bounded route overview, with runtime and cost budgets still green.
