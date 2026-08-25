# Experiment 017 — semantic landmark suite

## Hypothesis

Named-plot reservation filtering preserves Kentridge's semantic landmark definitions and their
canonical placement in the combined catalogue.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.KentridgeSemanticLandmarkTests` fixture through
`tools/unity-run.sh`.

## Result

`verification-semantic-landmark-results.xml` records exactly 1 test executed, 1 passed, 0 failed in
0.093 seconds. The guarded Unity process exited 0; its log is
`verification-semantic-landmark-unity.log`.

## What was learned

The hypothesis is confirmed. Landmark identity and placement remain authoritative and unchanged.

## Next

Run shape-program encoding coverage to verify compaction and finalization preserve catalogue data.
