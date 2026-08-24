# Experiment 019 — shape encoding suite

## Hypothesis

Compacting explicit placement slices and re-finalizing the catalogue preserves valid shape-program
encoding, ranges, and deterministic catalogue data.

## What was performed

After the prior Unity process fully disappeared, reran the full
`VoxelEngine.Tests.EditMode.KentridgeShapeProgramEncodingTests` fixture through
`tools/unity-run.sh` against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the
production/test diff.

## Result

`verification-shape-encoding-results.xml` records exactly 5 tests executed, 5 passed, 0 failed in
0.142 seconds. The guarded Unity process exited 0; its log is
`verification-shape-encoding-unity.log`.

## What was learned

The hypothesis is confirmed. The adapter's compaction and finalization preserve catalogue
structural validity and deterministic encoding.

## Next

Run the two PlayMode fixtures that directly build the combined catalogue.
