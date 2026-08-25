# Experiment 006 — final-catalogue foreground spatial diagnostic

**Question** — Which definitions in the final combined Kentridge catalogue actually survive into the saved camera's lower-town corridor? Attempts 1 and 2 both changed plausible stair systems without materially changing the exact replay, so class-name/source inspection was no longer sufficient.

**Method** — A temporary EditMode diagnostic built `KentridgeCombinedVoxelCatalogue` and logged every surviving explicit placement whose final X/Z envelope intersects x=560..1540 dm, z=820..1020 dm around the captured foreground. The second diagnostic run used `Debug.Log` so the Unity log preserved the enumeration.

**Evidence** — Actions run `32838138335`, source `a1e4003746ab9520245b026469b15bd28b780b31`, completed successfully. Artifact `9559453236` (`scene-220516-spatial-diagnostic`) has digest `sha256:a32443a5abc24b7d3524ca4f4311050cc647fa8bdf282be995785476720c91b1`.

Important surviving placements include:

- `kentridge-district-terrace-lower-residential-main`, precedence 15, x/z 584,864..1456,1126.
- `kentridge-district-terrace-lower-residential-east`, precedence 15, x/z 1424,814..1776,1086.
- Main road, precedence 54, x=1142..1198.
- Main-road sidewalk paint strips, precedence 59, x=1132..1144 and x=1196..1208.
- East-service sidewalk paint strips, precedence 59, x=1462..1474 and x=1506..1518.
- Residential-road surface and several stable named structures.

Crucially, **no `kentridge-access-*` placement survives in this captured corridor**, explaining why attempt 2 was a visual no-op. No anonymous `kentridge-fabric-*` placement owns the lower foreground stair bands either.

**Geometry trace** — `KentridgeUrbanSidewalkCatalogue` is paint-only: it uses `PrimitiveMode.PaintSurface`, so the sidewalk strips do not create the step geometry. `KentridgeDistrictTerraceCatalogue.AddShoulder`, however, explicitly constructs each neighborhood shoulder from `ShoulderStepCount = 6` carve/fill box slices. For `lower-residential-main`, the north shoulder spans z=864..900 dm and is crossed by the main/residential pedestrian paint. The six landform steps are therefore recolored as hard pedestrian surfaces and read as independent stair flights beside the primary climb. The same mechanism exists around the lower-residential-east/service-lane terrace.

**Conclusion** — The final production attempt should target the reusable district-terrace transition contract: preserve the flat terrace core and later road authority, but grade neighborhood shoulders continuously rather than authoring six giant terrain steps. Retaining masonry is a separate later infrastructure program and can remain independent of the underlying continuous landform.
