# Experiment 016 — production plot-pad owner

## Hypothesis
The persistent upper dirt/grass rectangle is owned by a later organic plot-surface placement, not the civic terrace/court experiments.

## Action / source
At feature source `bd6176c57d2ef70a4dc635c4c9d694399a30c280`, reproduced Kentridge planning with the exact serialized VoxelShowcase seed `1592594996` and reprojected upper capture envelope `X≈91.0..93.8m, Z≈28.6..30.4m`. Traced the active canonical plot-surface catalogue and compared its authored target with deterministic `TerrainQuery` heights.

## Result
Production places `MayorHouse` (`WideHouse`) at `X=91.0..104.2m, Z=25.0..38.2m`, directly containing the marked envelope. Its plot target is `221`; natural terrain at representative marked edge `(910,295)` is `223` (the marked area is roughly `222–224`). The generic plot program expands twelve higher moss-capped terraces to the parcel edge, reaching about `233–234`, roughly one metre above the real surrounding terrain. The earlier civic/court source changes produced pixel-identical marked-ground replays and therefore do not own the visible defect.

## Verdict / next
Supported. Make plot grading semantic to the building-envelope core, leave parcel-edge height to natural terrain, retain shallow foundation skirts for support, and add an exact-seed production-catalogue regression.