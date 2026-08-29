# Plan — 20260826-132234-356 VoxelShowcase

## Observed / acceptance
The exact `VoxelShowcase` capture (seed `1592594996`, saved camera pose in `issue.json`) marks two jagged Dirt/grass contacts. Both marked regions must be replayed in the built player and no longer show metre-scale right-angle bites. The scene must reach full residency without runtime exceptions.

## Discrimination history
- Civic terrace/court, isolated plot-cap shape, route shape, route/plot precedence, and macro-root hypotheses were tested against fresh saved-pose player artifacts and rejected when the marked ground stayed unchanged.
- Experiment 021 established that square/cylinder route and several plot-cap variants changed zero pixels in either marked circle and required testing the final combined writer rather than isolated geometry.
- Workflow `33276653432` then explicitly falsified Experiment 024: no production organic route stamp reaches the corrected upper marked envelope. Experiment 023/024 player captures are byte-identical over rendered terrain, so their cylinder/order changes are being reverted.
- Experiment 025 re-audits the final active organic-Kentridge stage. The saved camera sits on the MayorHouse frontage; its generated foundation footprint intersects both immutable camera probes and is emitted by the final shared-structure catalogue.

## Final-writer evidence
For seed `1592594996`, deterministic planning places `MayorHouse` at `(910,250)dm` in a `132x132dm` WideHouse envelope with orientation `2`. The generated MayorHouse form is `90x78dm`; its rotated foundation spans approximately `X=931..1020dm, Z=294..371dm`. Reconstructed marked probes near `(934,299)` and `(958,306)` therefore lie inside that final structure footprint.

`KentridgeDefinition.Theme` authors a `7dm` foundation. `KentridgeSharedStructureVoxelCatalogue` currently places every structure at `targetSurface - 5dm`, so generated houses lift their compiled foundation/floor datum `2dm` above the authored plot surface. Because the shared-structure stage runs last for organic Kentridge, that protruding rectangle can overwrite the earlier terrain/plot material in both marked contacts.

An earlier commit, `53f76db8b6629e1de7aa3a89750ab46403b4a3d1`, already encoded the generated-house foundation-depth sink but never received a CI status or built-player acceptance run. It was not actually visually falsified.

## Selected fix / regression
Restore that untested placement correction:
- generated shared houses sink by their compiler-authored foundation depth (`theme.FoundationHeightDm`, `7dm` for Kentridge);
- bespoke structures retain the established `5dm` sink;
- revert Experiment 023's cylinder route stamps to the pre-experiment square implementation;
- revert Experiment 024's organic-route stage reorder to the established pre-plot order.

The focused exact-seed regression now evaluates the real MayorHouse shared-structure definition and rasterizes the **final `KentridgeCombinedVoxelCatalogue`** into authoritative storage for the single region containing both saved-camera probes. It must prove:
- MayorHouse generated placement uses the full authored foundation sink;
- the production foundation horizontally overlaps both marked probes;
- final combined storage contains no Foundation material one voxel above the authored ground surface at either probe;
- Foundation support remains present one voxel below the surface at both probes.

## Blast radius / cost
Blast radius is limited to vertical placement of generated Kentridge shared houses. Plot geometry, route topology, route placement, bespoke landmarks, renderer, terrain, definition/placement/primitive counts, and per-frame behavior are unchanged. The generated house stack moves down by the existing 2dm foundation mismatch so its compiled floor datum aligns with authored ground; generation cost is unchanged.

## Remaining gates
Merge current `master` before the final request so the latest CI timeout/workflow changes are included. Then create one fresh targeted PlayMode request on `ci-test/fixes/agent-8` for the exact feature SHA and exact saved-pose `VoxelShowcase` replay. Inspect the focused regression, player logs, and both immutable marked regions. Only after a green exact-SHA workflow **and** green visual/runtime acceptance may the issue move through pending to closed and the exact feature head be pushed non-force to `master`.
