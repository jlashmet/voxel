# Plan — 20260826-132234-356 VoxelShowcase

## Observed / acceptance
The exact `VoxelShowcase` capture (seed `1592594996`, saved camera pose in `issue.json`) marks two jagged Dirt/grass contacts. Both marked regions must be replayed in the built player and no longer show metre-scale right-angle bites. The scene must reach full residency without runtime exceptions.

## Discrimination history
- Local civic terrace/court, plot-cap, precedence, generated-foundation, and macro-root hypotheses were tested against fresh saved-pose player artifacts and rejected or reverted when marked pixels stayed unchanged.
- Experiment 021 established that isolated route/plot/cap variants changed zero rendered-ground pixels in the circles and required identifying the *final winning writer* in the combined catalogue.
- Experiment 023 correctly traced the exact authored Showcase path to organic Kentridge circulation and replaced square road stamps with bounded radial cylinders, but workflow `33275090653` still showed the upper rectangular grass tongue. Radial roads alone were therefore visually inert.
- Experiment 024 identifies the missing ownership seam: `FeatureRegionBuild` walks concatenated rules in order rather than sorting by `Precedence`. Organic Kentridge previously concatenated roads before terrace support and plot surfaces, so rectangular Moss-topped parcel grading overwrote public access corridors afterward.

## Final-writer evidence
For seed `1592594996`, deterministic planning places `MayorHouse` at `(910,250)dm` as a `132x132dm` WideHouse with production orientation `2`. Its plot-surface core owns approximately `X=920..1035dm, Z=278..377dm` at the captured surface elevation. The corrected upper marked camera envelope is approximately `X=910..938dm, Z=286..304dm`, directly crossing that rectangular plot-surface owner. The semantic organic access route also traverses the parcel shoulder from the realized entrance toward the public network.

This ordering explains the prior byte-identical road experiments: the road could change shape while a later plot surface still won the visible material boundary.

## Selected fix / regression
For **organic Kentridge only**, canonical composition now runs:
1. ground cover;
2. terrace support;
3. plot surfaces;
4. organic circulation;
5. market piazza;
6. plot dressing;
7. town dressing;
8. shared structures.

Legacy/non-Kentridge circulation keeps its previous ordering. The market piazza remains after roads so its authored shared-space surface still wins. Organic route stamps retain the same points, widths, entrance connectors, terrain sampling, definition/placement counts, and two-primitive clear+surface budget, but use vertical cylinders instead of square boxes.

The focused exact-seed regression proves:
- VoxelShowcase uses the authored organic-route plan;
- production organic clear/surface primitives are vertical cylinders and exclude the old square corner;
- a production route stamp reaches the corrected upper marked envelope;
- in the final combined catalogue, plot grading precedes organic routes;
- the market piazza still follows organic routes.

## Blast radius / cost
Blast radius is limited to organic Kentridge circulation ownership/order. Legacy Kentridge roads, non-Kentridge settlements, plot geometry, buildings, macro-world routes/markers, renderer, and terrain are unchanged. Stage/definition/placement/primitive counts are unchanged. Cylinder footprints rasterize fewer cells than the prior squares; reordering adds no work, so generation cost is equal or lower.

## Remaining gates
Fetch current `master`, merge it into `fixes/agent-8` if it advanced, then create one fresh final targeted PlayMode request on `ci-test/fixes/agent-8` for the exact feature SHA and exact saved-pose `VoxelShowcase` replay. Inspect both immutable marked regions and runtime logs. Only after a green exact-SHA workflow **and** green visual/runtime acceptance may the issue move through pending to closed and the exact feature head be pushed non-force to `master`.
