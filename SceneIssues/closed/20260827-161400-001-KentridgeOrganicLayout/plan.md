# Plan: Organic Kentridge Layout

## Evidence and observations

- The capture contains no screenshot, markup/circle data, frame state, or runtime capture, so there are no marked visual regions to inspect or replay.
- The defect is semantic: authored road axes and street-facing placement made roads the macro-layout truth. The fix preserves all 17 stable gameplay roles while emitting zero `PlannedStreet` entries and inferring public routes from realized entrances and the market plaza.
- `KentridgeOrganicLayoutTests` is the behavioral regression. It covers role identity, no authored streets, non-cardinal connected circulation, no plot overlap, seed variation, semantic public approaches, local terrain grounding, realized entrance-to-route voxel coverage, preservation of all 17 structure programs, and bounded route catalogue size.

## Competing hypotheses

1. **Authored road topology is the root cause. Supported.** Removing only road rendering would leave placement/access road-led; the fix removes streets from Kentridge semantic planning and derives public route access from placed structures.
2. **This is only voxel-surface styling. Rejected.** The regression exercises semantic access and gameplay entrance resolution before voxel realization.
3. **Terrain grounding alone is sufficient. Rejected.** Local grounding prevents shelves/floating, but seed-varying placement and non-cardinal inferred circulation are independently required.

## Repro, fix, and validation

- Repro through production planning: build Kentridge and inspect `Streets`, `Routes`, plot access, role identity, terrain heights, and realized route coverage.
- Selected fix: bounded deterministic named-site placement by district envelope, street-independent public access, inferred connected route polylines, local terrain sampling, and route-driven production voxel realization. Generic legacy street support remains for other settlements.
- Verified source: `6c42356fd9204444c6fcda435e2e37cba1ed4c54`.
- Targeted CI: run `33180958474` passed `VoxelEngine.Tests.EditMode.KentridgeOrganicLayoutTests` with one executed case in 68 s.

## Blast radius, cost, remaining gate

- Architecture remains quarter-turn deterministic while public approach can be diagonal; migration is limited to settlement access/circulation boundaries.
- Placement is capped at 256 candidates per named site; route inference is bounded over 16 routed sites; voxel route sampling is asserted below 2,048 placements.
- Remaining gate: bookkeeping `pending -> closed`, merge current `origin/master`, and push the exact feature head to `origin/master` non-force.
