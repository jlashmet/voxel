# Experiment 024 — plot grading overwrites the authored organic road

## Falsified candidate
Exact-SHA request `agent8-132234-organic-road-final-f32b3513` ran as workflow `33275090653` against feature source `f32b3513f020e304db9db8cfabfd769110f3fef4`. The fresh Showcase bake, focused radial-road regression, real-player build, 45-second saved-camera replay, screenshot generation, and artifact upload all completed successfully. GitHub later concluded the job `cancelled`, so it was not a valid green gate. More importantly, direct inspection of the full-resolution `RealPlayer/verification-final.png` still shows the same large rectangular grass tongue in the upper immutable circle. The route-box-to-cylinder change therefore did not own the final visible material boundary by itself.

## Final-writer discriminator
`FeatureRegionBuild` does not sort explicit instances by `FeatureDefinition.Precedence`; it walks `catalogue.Rules` in concatenation order. Organic Kentridge's canonical stage order was:

1. ground cover
2. organic circulation
3. terrace support
4. plot surfaces
5. market piazza
6. plot dressing
7. town dressing
8. shared structures

That means the plot-surface stage ran *after* the public road and could replace its road material wherever a parcel shoulder overlapped an inferred entrance connector.

For seed `1592594996`, deterministic planning places `MayorHouse` at `(910,250)dm` as a `132x132dm` `WideHouse`, with production frontage orientation `2`. `KentridgePlotSurfaceCatalogue` gives a WideHouse a rectangular Moss-topped core and twelve rectangular feather terraces. After the real orientation, the core owns approximately `X=920..1035dm, Z=278..377dm` at the captured surface elevation. The corrected upper saved-camera envelope from Experiment 015 is `X=910..938dm, Z=286..304dm`, so the marked rectangle crosses the plot-surface owner itself. This explains why changing the earlier route primitive produced byte-identical marked ground.

The same semantic plan authors an organic access route from the realized MayorHouse entrance out through the parcel shoulder. The route is supposed to be the public circulation owner there; letting later plot grading repaint it creates the captured rectangular grass bite.

## Selected change
For organic Kentridge only, move `KentridgeDirectedTownSurfaceCatalogue` to immediately after terrace support + plot-surface grading and immediately before `KentridgeMarketPiazzaCatalogue`. Legacy/non-Kentridge ordering is unchanged. The market piazza, dressing, and structures still run later and retain their established ownership.

Keep the radial organic road stamps from Experiment 023. They were visually inert while plot grading won, but once the public road owns its final corridor they remove the old square-stamp corners without adding placements or primitives.

## Behavioral regression
The exact-seed PlayMode regression now proves both parts of the final-writer model:
- production organic route programs are bounded vertical cylinders, not square boxes;
- at least one production route stamp reaches the corrected upper marked envelope;
- in the real combined Kentridge catalogue, every plot-surface definition precedes the organic route definitions;
- the market piazza still follows the organic route definitions, preserving its later shared-space ownership.

## Blast radius / cost
Blast radius is limited to organic Kentridge composition order plus the already-bounded organic route primitive shape. Legacy directed-ramp Kentridge, non-Kentridge settlements, plot geometry, structures, macro-world instrumentation, renderer, and terrain are unchanged. The stage count, placement count, definition count, and primitive count are unchanged. Cylinders touch fewer X/Z cells than the previous square stamps, so generation work is equal or lower; reordering does not add runtime work.

## Acceptance
A fresh exact-SHA targeted PlayMode run must pass the ownership regression, build/replay the real `VoxelShowcase` saved pose, reach full residency without runtime exceptions, and visually remove the metre-scale rectangular Dirt/grass bite from both original marked regions. A green test without the visual result is still a rejection.
