# Experiment 007 — Hightown primitive contributor

## Hypothesis

Hightown's supposedly settlement-bound catalogue still includes a Kentridge-specific stage with
absolute Kentridge placements.

## What was performed

Against source commit `3d0923b829b41d337cdfe40af9677176865a2a1a`, enumerated every evaluated Hightown
primitive containing voxel `(1339,231,757)` while repeating the four-world composition probe.
Ran locally through `tools/unity-run.sh`.

## Result

The occupancy-producing contributor was
`kentridge-working-lane-block-court`, precedence 85, `Fill`, material 6, placement
`(1324,230,674)`. Lower-precedence Kentridge district-terrace and terrace-surface operations also
covered the voxel. Evidence is in `verification-hightown-contributor-results.xml` and
`verification-hightown-contributor-unity.log`.

## What was learned

The hypothesis is confirmed. `KentridgeUrbanCourtCatalogue` calls
`KentridgeUrbanOrganizer.Build(seed)` unconditionally; when the canonical pass is bound to Hightown,
it still emits the Kentridge working-lane court through the pub. Several other canonical stages have
the same Kentridge-only placement contract.

## Next

Add a catalogue-boundary regression that rejects every Hightown placement on Kentridge's side of the
country midpoint, then use it to define the complete Kentridge-only stage boundary.
