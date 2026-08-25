# Experiment 012 — full two-town regression

## Hypothesis

Separating Kentridge-owned stages from the shared settlement stages preserves the established
two-town world invariants while enforcing the new Hightown boundary.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted fix, ran the
complete `VoxelEngine.Tests.EditMode.TwoTownWorldTests` fixture locally through
`tools/unity-run.sh`.

## Result

All 9/9 tests passed. Evidence is in `verification-two-town-fixture-results.xml` and
`verification-two-town-fixture-unity.log`.

## What was learned

The hypothesis is confirmed for the tested country-plan, corridor, bridge, material, and catalogue
boundary invariants.

## Next

Run the Kentridge generation fixture to verify its previously complete authored sequence remains
unchanged and valid.
