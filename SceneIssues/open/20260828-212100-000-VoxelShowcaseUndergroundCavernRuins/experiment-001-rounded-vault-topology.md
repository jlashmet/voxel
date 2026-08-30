# Experiment 001 — Rounded vault passage topology

## Hypothesis
The failed built-player corridor silhouette is caused by primitive topology, not sampling cadence or material selection. The generic cave route exposes a rectangular safety core; later cavern passes union vertical cylinders whose walls are vertical and caps are planar, so variable spacing alone cannot reliably hide the box/rib silhouette.

## Baseline discriminator
Exact run `33284693031` / source `492ea820...` was functionally green (38/38 route waypoints, zero harness assertions, 34,798,060 total writes, 4,416,056 naturalization writes, 8 total lights) but every useful underground frame still showed repeated vertical ribs/planar ceiling bands and the destination read as a rectangular masonry throat. That falsified cadence-only, movement, removed-box, and capture-window explanations.

## Production change
On `fixes/agent-3`, introduce one deterministic rounded-vault profile whose clearance slices never shrink below a mathematically derived radius that covers the rectangular route core halfway between maximum-spaced nodes. Vary wall radius vertically and taper a crown to a one-voxel apex. Reuse the same profile for full-route naturalization, doglegs, and destination circulation. Preserve the generic cave core, public authoring API, renderer, material catalogue, camera, floor support, lights, and normal CharacterMotor path.

For cost, emit the stacked-disc profile as equivalent contiguous `FillColumnBulk` radial spans rather than slow per-voxel disc writes; geometry is unchanged while retaining the existing batched authoring path.

## Regression / expected result
Focused production tests require deterministic repeated profiles, multiple wall radii, tapered crown, worst-case adjacent-node cover of the rectangular core, normal WorldBuilder generation, normal CharacterMotor traversal, <=15M naturalization writes, <=55M total writes, and <=8 lights.

**Expected rendered discriminator:** the long descent no longer reads as repeated vertical cylinders or a rectangular hallway; the player reaches a large irregular dark cavern and the final composition clearly reads as aged ruin plus exactly two grounded flanking humanoid statues.

## Exact result — rejected
Canonical transport `2afc0626968adcb1d858bc7a21925b50225f5563` validated exact feature source `263b6667f3f98ff9a8f580403e7ec95540aeebf8` in workflow run `33286541699` / job `99190592084`. Focused PlayMode and the standalone player both passed. Production metrics were:

- 35,166,289 total writes (+368,229 / +1.06% from the failed visual baseline)
- 4,792,841 naturalization writes across 215 nodes (+376,785 / +8.53%)
- 3,580,112 visual-finish writes
- 20 preloaded regions
- 6 route lights / 8 total lights
- 39 semantic route points; standalone player reached waypoint 38/38
- zero harness assertions
- steady-state FPS samples after startup: minimum 46.4, median 92.8, mean 109.3; the isolated 46.4 sample coincided with a 210.67 ms streaming/admission spike
- renderer arena telemetry peaked at 18,969,600 allocated vertex slots, 28,677,632 index slots, 2,405 draw leases, zero lease failures; visible-region `drawn` peaked at 520 and was 274 near the destination. These arena counters are allocation/lease telemetry, not a direct visible-triangle count.

Direct review of all seven exact standalone-player frames **failed the expected rendered discriminator**. The rounded-vault change visibly removed the old flat cylinder caps: underground ceilings now form stepped/concentric rounded crowns. However:

1. Frames 1–5 still read as a repetitive masonry-lined tube with strong vertical ribbing/terracing and architectural tiled surfaces rather than geological cavern walls.
2. The rounded crown is itself visibly periodic/terraced, so changing vertical-cylinder topology was only a partial presentation improvement.
3. The final frame is a huge but strongly rectangular room with flat vertical walls and a straight paved/railed approach, not a huge irregular natural cavern.
4. The aged ruin and exactly two grounded humanoid statues are not clearly readable as a flanking destination composition in any captured frame.
5. Lighting remains localized/dark enough for navigation, and production traversal/cost are healthy, but those functional successes do not satisfy the visual acceptance criteria.

**Conclusion:** primitive topology was a real contributor to the flat ceiling, but it was not the dominant remaining cause of the architectural presentation. The next product repair must trace and replace the owning layer that leaves masonry/tiled natural-cave surfaces and the rectangular destination host/approach, and must compose the ruin plus exactly two statues so they are unmistakably visible from the normal player route. This experiment is rejected for closure.
