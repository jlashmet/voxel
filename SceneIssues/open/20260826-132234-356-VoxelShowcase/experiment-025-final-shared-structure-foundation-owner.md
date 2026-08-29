# Experiment 025 — final shared-structure foundation owner restored

## Question
The latest exact request disproved Experiment 024 before its visual gate: no production organic-route stamp reaches the corrected upper marked envelope. Which late production stage actually owns both marked contacts?

## Runtime/source discrimination
- Workflow `33276653432` failed the focused regression because zero production organic route stamps intersect the corrected upper envelope `X=910..938dm, Z=286..304dm`.
- Full-resolution comparison of the Experiment 023 and 024 player captures shows the rendered terrain below the sky/tree band is byte-identical; moving the organic road after plot grading did not change either marked contact.
- Exact seed `1592594996` places `MayorHouse` at `(910,250)dm` in a `132x132dm` WideHouse envelope with orientation `2`. Its generated `90x78dm` house foundation rotates to approximately `X=931..1020dm, Z=294..371dm`, intersecting both saved-camera probes near `(934,299)` and `(958,306)`.
- `KentridgeSharedStructureVoxelCatalogue` is the final active organic-Kentridge stage. The Kentridge theme authors a `7dm` generated foundation, but production placement currently sinks every structure by only `5dm`. That lifts the generated foundation/floor stack `2dm` above the authored plot surface exactly at the marked frontage.

## Prior candidate audit
Commit `53f76db8b6629e1de7aa3a89750ab46403b4a3d1` already encoded the generated-house correction: generated programs sink by `theme.FoundationHeightDm`, while bespoke programs retain their established `5dm` sink. It also added an authoritative-storage foundation regression. That commit has no published commit status and no corresponding final request in `ci-operations.md`; therefore the candidate was abandoned before the required exact-SHA regression/player gate and was never visually falsified.

## Selected discriminator
Restore the untested foundation-depth placement correction and revert the now-falsified Experiment 023/024 production changes:
- organic route stamps return to their pre-experiment square implementation;
- organic circulation returns to its established pre-plot ordering;
- generated shared houses sink by their compiler-authored foundation depth (`7dm` for Kentridge); bespoke structures retain `5dm`.

The replacement regression now rasterizes `KentridgeCombinedVoxelCatalogue` into authoritative storage for the region containing both immutable camera probes. It requires the production MayorHouse foundation footprint to overlap those probes horizontally, requires Foundation material to be absent one voxel above the authored surface in the final combined result, and requires Foundation support to remain one voxel below it.

## Blast radius / cost
Only vertical placement of generated Kentridge shared houses changes: the whole generated house stack moves down by the 2dm mismatch so its compiled floor datum meets the authored plot surface. Plot geometry, route topology/shape, bespoke landmarks, renderer, terrain, definition counts, primitive counts, and per-frame runtime are unchanged. Generation cost is unchanged.

## Acceptance gate
Run the focused PlayMode regression and a forced fresh `VoxelShowcase` bake/player replay at the exact feature SHA. Both original circles must lose the metre-scale rectangular/jagged Dirt/grass contact. A green storage regression without visual improvement remains a rejection.
