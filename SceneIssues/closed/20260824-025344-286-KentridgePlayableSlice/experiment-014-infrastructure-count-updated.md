# Experiment 014 — infrastructure count updated

## Hypothesis

Pinning the deterministic conflict-free combined infrastructure count at 39 will preserve all
stronger stage-local inventory and stable-gameplay-structure checks while making the fixture agree
with the intentional reservation policy.

## What was performed

Updated only the final combined-count expectation and its diagnostic message, then reran
`VoxelEngine.Tests.EditMode.KentridgeInfrastructureTests` through `tools/unity-run.sh` against
source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff.

## Result

`verification-infrastructure-fixed-results.xml` records exactly 1 test executed, 1 passed,
0 failed in 0.089 seconds. The stage-local counts, exact 17 gameplay structures, and exact 39
conflict-free combined infrastructure instances all passed. The guarded Unity process exited 0;
its log is `verification-infrastructure-fixed-unity.log`.

## What was learned

The hypothesis is confirmed. Coverage was not weakened: the test still pins every unfiltered stage
inventory and now pins the intentional filtered composition exactly.

## Next

Run every remaining EditMode fixture that directly builds the combined Kentridge catalogue.
