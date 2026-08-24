# Experiment 009 — clean spacing regression

## Hypothesis

The retained named-plot spacing regression will still pass after all temporary diagnostic tests
and replay camera assets are removed.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran only
`VoxelEngine.Tests.EditMode.KentridgeUrbanOrganizationTests.SecondaryUrbanPlacementsRespectNamedPlotSpacing`
in EditMode through `tools/unity-run.sh`.

## Result

`verification-final-spacing-results.xml` records exactly 1 test executed, 1 passed, 0 failed in
0.055 seconds. The guarded Unity process exited 0; its log is
`verification-final-spacing-unity.log`.

## What was learned

The hypothesis is confirmed. The invariant is enforced by retained production and test code, not
by any temporary diagnostic or replay fixture.

## Next

Run the complete Kentridge urban-organization fixture and the affected catalogue-generation tests.
