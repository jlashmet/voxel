# Experiment 013 — infrastructure count contract

## Hypothesis

The existing Kentridge infrastructure fixture will accept the reservation-filtered combined
catalogue because its semantic stage catalogues and 17 stable gameplay structures are unchanged.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.KentridgeInfrastructureTests` fixture through `tools/unity-run.sh`.

## Result

The hypothesis is disproven. `verification-infrastructure-results.xml` records exactly 1 test
executed and failed: the stage-local counts and the 17-structure invariant passed, but the final
assertion expected the old combined infrastructure count of 105 and measured 39 after named-plot
reservations. The guarded Unity process exited 2; its log is
`verification-infrastructure-unity.log`.

## What was learned

The failure is an expected stale composition assertion, not evidence that a named building or
stage definition disappeared. The exact combined count deliberately includes every secondary
instance that this fix filters; retaining 105 would require restoring the proven overlaps. The
contract should instead pin the new conflict-free combined count and explain why it differs from
the unfiltered stage-local inventories.

## Next

Update the fixture's combined-count assertion to the measured conflict-free count while preserving
all stage-local inventory and 17-gameplay-structure assertions, then rerun it.
