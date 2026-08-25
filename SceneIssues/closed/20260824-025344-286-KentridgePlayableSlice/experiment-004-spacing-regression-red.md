# Experiment 004 — named-plot spacing regression (red)

## Hypothesis

A permanent regression over active canonical placements can express the missing authoring invariant:
secondary urban building envelopes must stay outside every stable named plot envelope expanded by
Kentridge's declared 12 dm minimum spacing.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the new retained test
`VoxelEngine.Tests.EditMode.KentridgeUrbanOrganizationTests.SecondaryUrbanPlacementsRespectNamedPlotSpacing`,
built the canonical catalogue, transformed each active secondary footprint by its cardinal
orientation, and compared it to all 17 named plot reservations. Ran the single test through
`tools/unity-run.sh`.

## Result

Exactly 1 test ran and failed as expected. The first violation was
`kentridge-vertical-0` at `(690,211,766)` entering Logan House's expanded reservation
`(1002,700)..(1130,828)`. Evidence is `verification-spacing-regression-red-results.xml` and
`verification-spacing-regression-red-unity.log`.

## What was learned

The hypothesis is confirmed. The regression catches the systematic authoring failure at the
declared-placement level, before rendering or occupancy precedence can hide it, and uses the
existing settlement density policy rather than inventing a camera-specific gap.

## Next

Add one deterministic canonical lowering adapter that removes conflicting secondary explicit
placements against the named plot reservations, apply it to every secondary urban building stage,
then rerun this regression and the occupied-cell diagnostic.
