# Experiment 008 — Hightown placement boundary

## Hypothesis

The working-lane court is one instance of a wider failure: the Hightown catalogue includes many
Kentridge-only stages rather than one accidental placement.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, added an EditMode regression that
builds Hightown's voxel catalogue and rejects any explicit placement south of the midpoint between
the two settlement centres. Ran the single test locally through `tools/unity-run.sh`.

## Result

The test failed 0/1 and listed broad Kentridge-only groups: district terraces and corrections,
processional climb, vertical connectors, sidewalks, civic forecourt, street dressing, urban courts,
anonymous fabric, galleries, skybridge, access works, and hillside architecture. The full list is
in `verification-hightown-boundary-results.xml`; Unity output is in
`verification-hightown-boundary-unity.log`.

## What was learned

The hypothesis is confirmed. Removing only the working-lane court would leave a structurally broken
two-town catalogue. The canonical builder must distinguish settlement-bound common stages from
Kentridge-only authored urban stages.

## Next

Gate all Kentridge-only stages at the canonical composition boundary while retaining Hightown's
plan-bound ground cover, streets, plot preparation/dressing, frontage paths, market, foundations,
and structures.
