# Plan

## Captured evidence
One capture, two marked Dirt/grass contacts, exact saved camera pose at 1928×836. The lower mark is on organic circulation. Correct camera-ray reconstruction places the upper marked envelope at approximately `X=91.0..93.8m, Z=28.6..30.4m` beside MayorHouse.

Exact built-player replay `33231174953` for feature source `ed9334667bee230900a1012770ee9749b31d7e0f` freshly baked `ShowcaseWorld.bytes`, passed the focused PlayMode test, built `VoxelShowcase`, replayed the saved camera, and reached stable residency. The workflow was later cancelled by aggregate timeout, so it is not a green gate. Direct artifact inspection shows the lower mark improved but the upper mark still has a metre-scale axis-aligned grass edge.

## Competing hypotheses / evidence
1. **Square organic-route stamps** — supported for the lower mark. Replacing square carve/fill stamps with equal-width cylinders changed that contact without moving routes or increasing its two-primitive budget. Keep this fix.
2. **Route ownership of the upper mark** — falsified by exact-seed placement regression and route-only replay.
3. **Plot/route numeric precedence** — falsified: finalisation does not sort by precedence; generation follows combined rule order.
4. **Generated pad shrink alone** — falsified by byte-identical marked pixels after matching the shared-house foundation rectangle.
5. **Fixed 1.2m rounded Moss cap** — partially supported then falsified as sufficient. The built replay shows the former southwest corner at `(92.7m,28.6m)` is now Dirt, proving material ownership changed, but the 1.2m tangent reaches `Z≈29.8m`, inside the original upper mark, where the boundary becomes straight again.
6. **Stale bake / streaming** — falsified by repeated forced bakes and stable real-player residency.

## Fix / behavioral regression
Keep the round route stamps. For organic Kentridge generated houses only, keep the exact rectangular Dirt support, elevation, clearance, placement, and foundation alignment. Replace the small rounded-box Moss paint with a plan-view stadium cap: one thin `PaintSurface` bridge plus two vertical circular `PaintSurface` end-caps using the largest contained integer radius. This moves MayorHouse's side tangent outside the captured envelope without changing occupancy.

The exact-seed regression evaluates MayorHouse through production `ShapeProgram` orientation. It asserts unchanged support `927..1024 × 286..371dm` at `Y=221dm`, five bounded primitives, Dirt ownership at the captured old straight-edge probe `(927,221,300)`, Moss at `(938,221,304)`, and round organic-route stamps.

## Blast radius / cost
Only organic Kentridge generated-house visible Moss ownership changes. Bespoke/legacy pads, support occupancy, structures, routes, elevation, and clearance are unchanged. Generated pads rise from three to five bounded bake primitives; paint is one voxel deep and remains prebaked, with no per-frame cost.

Remaining gates: final targeted exact-SHA PlayMode CI, forced fresh showcase bake, real-player build, immutable camera replay, and direct verification that both original circles contain no metre-scale rectangular/stair-step Dirt/grass contacts.
