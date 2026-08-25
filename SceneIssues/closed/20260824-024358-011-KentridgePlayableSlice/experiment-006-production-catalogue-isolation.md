# Experiment 006 — production catalogue isolation

## Hypothesis

One of the additional catalogues in the production two-town world authors the torso obstruction
that the Kentridge-only test does not contain.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, built and sampled four generated
worlds at voxel `(1339,231,757)`: Kentridge only, Kentridge+Hightown,
Kentridge+corridor, and the full production combination. The production seed, plans, hidden-space
plan, and scene material mappings were used. Ran locally through `tools/unity-run.sh`.

## Result

The material tuple was `k=0 kh=6 kc=0 khc=6`. Kentridge and the corridor leave the torso voxel
empty; adding Hightown makes it material 6. Evidence is in
`verification-catalogue-isolation-results.xml` and
`verification-catalogue-isolation-unity.log`.

## What was learned

The hypothesis is confirmed and Hightown is the isolated source. This is invalid cross-settlement
authoring: Hightown is approximately 400 m north and must not modify Kentridge's pub.

## Next

Enumerate Hightown shape-program primitives containing the exact torso voxel and identify the
settlement-generalization leak.
