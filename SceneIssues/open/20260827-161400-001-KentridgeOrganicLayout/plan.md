# Plan: Organic Kentridge Layout

## Evidence and observations

- The capture folder contains only `issue.json`, `plan.md`, and `tasks.md`; there is no screenshot, markup/circle data, frame state, or runtime capture to inspect. There are therefore no marked visual regions to invent conclusions from.
- The recorded defect is semantic: Kentridge's authored road axes and street-facing placement made roads the macro-layout truth. The resumed candidate instead preserves all 17 stable gameplay roles while emitting zero `PlannedStreet` entries and inferring public routes from realized entrances and the market plaza.
- `KentridgeOrganicLayoutTests` is the behavioral regression. It verifies stable role identity, no authored streets, non-cardinal inferred circulation, connected public-network terminals, no plot overlap, meaningful seed variation, semantic public approaches, local production-terrain grounding, realized entrance-to-route voxel coverage, preservation of all 17 structure programs, and bounded organic route catalogue size.
- Production voxel realization consumes inferred routes through `KentridgeDirectedTownSurfaceCatalogue`; organic plots and the market plaza sample local `TerrainQuery` height instead of the retired fixed district shelves.

## Competing hypotheses

1. **Authored road topology is the root cause. Supported.** Removing only road rendering would leave site placement/access semantics road-led; the candidate removes streets from the Kentridge semantic plan and derives route access from placed structures.
2. **This is only a voxel-surface styling defect. Rejected.** The regression exercises semantic access and gameplay entrance resolution before checking voxel realization, so a cosmetic reskin cannot satisfy it.
3. **Terrain grounding alone fixes the synthetic layout. Rejected.** Local terrain grounding prevents floating/shelved organic sites, but seed-varying district placement and inferred non-cardinal circulation are independently required and covered.

## Repro and validation

- Semantic repro: build the Kentridge plan for the regression seed and inspect `Streets`, `Routes`, plot access, and role identity.
- Behavioral regression: `VoxelEngine.Tests.EditMode.KentridgeOrganicLayoutTests` exercises the production planner, gameplay site-access resolver, terrain query, directed circulation catalogue, market piazza catalogue, and combined Kentridge catalogue in one fixture.
- Final gate: refresh `fixes/agent-3` from current `origin/master`, then run exactly that fixture through the isolated `ci-test/fixes/agent-3` transport. Promote metadata only after terminal green for the exact feature SHA.

## Blast radius and cost

- Generic `PlannedStreet`/legacy street support remains available for other settlements; Kentridge switches through additive route/access semantics and Kentridge-specific catalogue selection.
- Architecture remains quarter-turn deterministic while public approach can be diagonal, limiting migration impact to settlement access/circulation boundaries.
- Planner work is bounded to 256 placement candidates per named site. Route inference is linear in the 16 routed named sites with a bounded nearest-terminal scan; voxel route sampling is explicitly asserted below 2,048 placements in the regression.
