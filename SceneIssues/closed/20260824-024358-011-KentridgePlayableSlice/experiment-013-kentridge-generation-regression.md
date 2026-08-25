# Experiment 013 — Kentridge generation regression

## Hypothesis

Theme-gating Kentridge-owned stages preserves Kentridge's complete authored generation behavior and
only changes the Hightown invocation of the canonical composer.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a` plus the uncommitted fix, ran the
complete `VoxelEngine.Tests.EditMode.KentridgeGenerationTests` fixture locally through
`tools/unity-run.sh`.

## Result

All 10/10 tests passed. Evidence is in `verification-kentridge-generation-results.xml` and
`verification-kentridge-generation-unity.log`.

## What was learned

The hypothesis is confirmed for Kentridge's combined catalogue, plot surfaces, structures,
dressing, and authored placement invariants covered by the fixture.

## Next

Run the opening's generated-site regression, then build and replay the production player at the
saved issue pose.
