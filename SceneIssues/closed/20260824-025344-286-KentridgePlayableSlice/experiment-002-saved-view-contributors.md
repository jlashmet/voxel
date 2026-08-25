# Experiment 002 — saved-view catalogue contributors

## Hypothesis

The crowded facade and apparent reversed stair are authored by identifiable Kentridge catalogue
instances, rather than a half-voxel surface reconstruction or GPU meshing error.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus a temporary diagnostic in
`KentridgeGenerationTests`, evaluated the canonical Kentridge catalogue and traced four saved-camera
rays through the centre, upper stair, left frontage, and right frontage. The diagnostic also listed
evaluated instance bounds intersecting the near-view volume. Ran
`VoxelEngine.Tests.EditMode.KentridgeGenerationTests.DiagnosticSavedOverlapViewContributors`
through `tools/unity-run.sh`.

## Result

The test passed 1/1 and produced 60 fill-ray hits plus 241 near-volume instance rows. The relevant
near contributors are:

- `kentridge-role-rebeccahouse`, placement `(1266,247,374)`, precedence 100;
- `kentridge-fabric-19`, `-20`, and `-21`, precedence 86;
- `kentridge-vertical-2`, precedence 85;
- `kentridge-infrastructure-retaining-gallery`, precedence 92; and
- `kentridge-access-upper-east-block-access`, precedence 94.

The saved centre ray meets multiple individual step/cheek/landing boxes from the access instance,
while upper and side rays meet Rebecca's shell and the anonymous fabric. Evidence is
`verification-view-contributors-results.xml` and `verification-view-contributors-unity.log`.

## What was learned

The hypothesis is confirmed. The apparent stair is the deterministic upper-east block access stair,
not a stepped GPU surface. The view contains a named gameplay house, anonymous block fabric,
vertical frontage, retaining gallery, and block access authored into the same compact volume. The
half-voxel boundary-field finding is therefore not causal.

## Next

Measure final per-instance occupied-cell intersections between named Kentridge structures and the
secondary urban fabric/access/gallery stages to determine whether this is a local Rebecca conflict
or a systematic missing reservation invariant.
