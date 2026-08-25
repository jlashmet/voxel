# Experiment 012 — two-town world suite

## Hypothesis

The modified canonical Kentridge catalogue remains compatible with the broader two-town world:
every recovered named Kentridge building remains placed, the towns remain separate, and Hightown
and the connecting corridor remain unaffected.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.TwoTownWorldTests` fixture through `tools/unity-run.sh`.

## Result

`verification-two-town-world-results.xml` records exactly 9 tests executed, 9 passed, 0 failed in
0.099 seconds. This includes `KentridgeStillPlacesEveryRecoveredBuilding`, town separation,
distinctness, bridge, and corridor coverage. The guarded Unity process exited 0; its log is
`verification-two-town-world-unity.log`.

## What was learned

The hypothesis is confirmed. The spacing fix preserves the named Kentridge settlement and does not
alter the other town or country corridor boundaries.

## Next

Review the complete production/test diff and determine whether any narrower infrastructure fixture
directly covers the filtered stages before finalizing evidence.
