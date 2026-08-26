# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
The capture note is `there is a floating mailbox`; there are no annotation circles, so the entire saved pose is the acceptance region. Camera `(152.18 m, 25.25 m, 59.43 m)` looks almost directly at the east market lamp at `(153.0 m, *, 54.9 m)`, about 4.6 m away. Accept only when the saved-camera replay shows that lamp visibly supported from road surface to lantern head, with no nearby streetscape regression.

## Competing hypotheses / discriminator
1. **Wrong lamp elevation / later sidewalk overwrite.** Falsified: lamp Y and the vertical road top both use `KentridgeVerticalProfile.SurfaceYAtDm`; sidewalk dressing is precedence 59 while lamps are 80.
2. **A plot sign/mailbox is floating.** Falsified for this capture: the nearest market sign is ~27 m away and outside the saved horizontal view; the east market lamp is ~7° from camera forward.
3. **The lamp pole inherits an unsuitable smooth surface style.** Selected. `ShowcaseWorld` registers dark stone (material 6) as `Smooth`; `LampProgram` uses it for a 3×3-voxel, 29-voxel-tall pole beneath a 7×7 lantern head. Thin smoothed support can visually collapse while the head remains.

## Fix / regression / blast radius
Give only the Kentridge lamp pole an explicit `Planar` surface override, preserving its dark-stone material and occupancy. Regression builds the production street-dressing catalogue, evaluates the exact `(1530, 549)` lamp, and requires its tall dark support primitive to be planar and vertically overlap the lantern.

Blast radius: all 24 Kentridge street lamps only; no placement, occupancy, collision, material palette, road generation, benches, planters, or renderer-wide behavior changes. Cost: one existing primitive carries a nonzero style id; no added primitives, allocations, storage reads, or jobs.

## Remaining gates
Implement regression + fix; green exact-SHA targeted CI with 30 s saved-pose replay; inspect final artifact; commit `verification-final.png` and pending metadata; separate `open -> pending`, then approved `pending -> closed`; merge current master and advance master non-force.
