# Experiment 018 — shape encoding wrapper refusal

## Hypothesis

The next isolated Unity test run can begin immediately after the semantic-landmark process reports
completion.

## What was performed

Attempted to start `VoxelEngine.Tests.EditMode.KentridgeShapeProgramEncodingTests` through
`tools/unity-run.sh` against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the
production/test diff.

## Result

No Unity test ran. The wrapper exited 3 before launch because PID 21892 still matched a Unity
editor process. A subsequent process query found no live Unity process, showing it was a brief
shutdown race from the preceding isolated run. No results or Unity log file was produced.

## What was learned

The hypothesis is disproven: a successful wrapper exit can briefly precede disappearance of the
editor process from the system process table. The concurrency guard worked as designed, and must
not be bypassed.

## Next

Retry the same isolated shape-encoding fixture now that no Unity process remains.
