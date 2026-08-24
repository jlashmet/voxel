# Experiment 005 — canonical named-plot reservations

## Hypothesis

Filtering only secondary urban explicit placements at the canonical composition seam, using their
declared cardinal footprints and the existing named-plot-plus-12-dm reservations, will enforce
spacing without moving stable roles, streets, plazas, or changing the GPU renderer.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, added
`KentridgeNamedPlotReservationCatalogue`. The canonical Kentridge composer applies it to urban
courts, vertical frontages, frontage-aligned anonymous fabric, galleries, the upper skybridge,
block access, and hillside architecture before combining them with named structures. The adapter
compacts each explicit-placement slice deterministically and clears removed entries so catalogue
identity changes with the authored world. Reran the focused spacing regression locally.

## Result

Exactly 1/1 tests passed in 0.054 seconds. No active secondary urban declared footprint enters any
of the 17 named plot envelopes expanded by the composition policy's 12 dm minimum spacing. Evidence
is `verification-spacing-regression-fixed-results.xml` and
`verification-spacing-regression-fixed-unity.log`.

## What was learned

The hypothesis is confirmed at the authoring contract level. The fix preserves the semantic town
plan and stable gameplay plots while preventing later infill/circulation layers from treating those
plots as vacant. It changes authoritative CPU catalogue occupancy only; GPU presentation remains
derived from the corrected cells.

## Next

Rerun the occupied-cell overlap diagnostic to prove all 28 measured collisions are gone, then build
and inspect the exact production-player replay before broadening validation.
