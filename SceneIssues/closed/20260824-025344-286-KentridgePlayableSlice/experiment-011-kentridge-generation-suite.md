# Experiment 011 — Kentridge generation suite

## Hypothesis

Applying reservations to the combined voxel catalogue preserves Kentridge's deterministic plan,
semantic plots, terrain support, vertical profile, dressing, and successful evaluation of every
remaining planned feature.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.KentridgeGenerationTests` fixture through `tools/unity-run.sh`.

## Result

`verification-kentridge-generation-results.xml` records exactly 10 tests executed, 10 passed,
0 failed in 0.127 seconds. The suite includes catalogue evaluation, deterministic-plan, semantic
plot support, town-climb, district shelf, foundation, and dressing coverage. The guarded Unity
process exited 0; its log is `verification-kentridge-generation-unity.log`.

## What was learned

The hypothesis is confirmed. The adapter changes only conflicting secondary placements in the
combined voxel catalogue; it does not mutate the authoritative settlement plan or prevent any
retained feature from evaluating.

## Next

Run the combined two-town catalogue suite, which is the broader consumer of the modified canonical
builder.
