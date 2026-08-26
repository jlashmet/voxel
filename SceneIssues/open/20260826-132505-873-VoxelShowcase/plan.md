# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
The capture note is `there is a floating mailbox`; there are no annotation circles, so the entire saved pose is the acceptance region. Camera `(152.18 m, 25.25 m, 59.43 m)` looks almost directly at the east market lamp at `(153.0 m, *, 54.9 m)`, about 4.6 m away. Accept only when the saved-camera replay shows that lamp visibly supported from road surface to lantern head, with no nearby streetscape regression.

## Competing hypotheses / discriminator
1. **Wrong lamp elevation / later sidewalk overwrite.** Falsified: lamp Y and the vertical road top both use `KentridgeVerticalProfile.SurfaceYAtDm`; sidewalk dressing is precedence 59 while lamps are 80.
2. **A plot sign/mailbox is floating.** Falsified for this capture: the nearest market sign is ~27 m away and outside the saved horizontal view; the east market lamp is ~7° from camera forward.
3. **The lamp pole inherits an unsuitable smooth surface style.** Selected. `ShowcaseWorld` registers dark stone (material 6) as `Smooth`; `LampProgram` used it for a 3×3-voxel, 29-voxel-tall pole beneath a 7×7 lantern head.

## Fix / regression / blast radius
The lamp pole now explicitly uses `SurfaceStyles.Planar`, preserving its dark-stone material, dimensions, occupancy, and placement. `KentridgeStreetLampSupportPlayTests.CapturedEastMarketLampKeepsPlanarSupportUnderLantern` builds the production catalogue, resolves/evaluates the exact `(1530, 549)` lamp, and requires its dark support to be planar and physically overlap the lantern.

Code/test head: `c3f8bfebbc1695574a019317094892a2c34b10b5`.

Blast radius: all 24 Kentridge street lamp poles only; no placement, occupancy, collision, material palette, road generation, benches, planters, or renderer-wide behavior changes. Cost: one existing primitive carries a nonzero style id; no added primitives, allocations, storage reads, or jobs.

## Remaining gates
Green exact-SHA PlayMode CI with 30 s saved-pose replay; inspect final artifact; commit `verification-final.png` and pending metadata; separate `open -> pending`, then approved `pending -> closed`; merge current master and advance master non-force.
