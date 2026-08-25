# Experiment 015 — architecture geometry suite

## Hypothesis

Filtering conflicting explicit placements leaves the combined catalogue's retained architectural
definitions and primitive geometry valid.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.ArchitectureGeometryCatalogueTests` fixture through
`tools/unity-run.sh`.

## Result

`verification-architecture-geometry-results.xml` records exactly 6 tests executed, 6 passed,
0 failed in 0.097 seconds. The guarded Unity process exited 0; its log is
`verification-architecture-geometry-unity.log`.

## What was learned

The hypothesis is confirmed. The reservation adapter compacts placement slices without corrupting
the shape definitions or retained generated geometry.

## Next

Run the combined-catalogue campaign-world realization fixture.
