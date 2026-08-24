# Experiment 010 — urban organization suite

## Hypothesis

Named-plot reservations preserve Kentridge's existing block-turning, interior-void, frontage-access,
and main-ascent organization invariants while adding the spacing invariant.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.KentridgeUrbanOrganizationTests` fixture through
`tools/unity-run.sh`.

## Result

`verification-urban-organization-results.xml` records exactly 4 tests executed, 4 passed, 0 failed
in 0.090 seconds. This includes all three pre-existing organization tests and the new spacing
regression. The guarded Unity process exited 0; its log is
`verification-urban-organization-unity.log`.

## What was learned

The hypothesis is confirmed. Filtering conflicting secondary placements does not close the main
ascent, remove declared access/voids from planned blocks, or turn coarse massing into gameplay
structures.

## Next

Run catalogue-generation coverage against the final clean source tree.
