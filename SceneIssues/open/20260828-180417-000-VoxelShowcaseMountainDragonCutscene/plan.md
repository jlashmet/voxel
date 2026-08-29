# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The prior built replay exposed X/Z-only false arrivals and an actually blocked ascent. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height. The reusable mountain program now orders scenic/support fills -> `Carve` clearance -> restored path floors, and replay requires grounded feet at +4.6 m tiers and +28.0 m summit. Vertical clearance remains 24 voxels / 2.4 m.

## Bake-cost discriminator
The final request `ee738bc85111...` for source `7b5393736485...` failed twice in the pre-test bake, so the requested acceptance test never ran. Retry logs show cold import consumed ~63 s, leaving ~177 s in actual baking before the 240 s Unity guard killed it.

Competing hypotheses:
1. Expanded feature bounds added a whole region layer. **Falsified:** only Y grew by 24 voxels; at the observed mountain base both old and new bounds already occupy the same 512-voxel layers, while X/Z are unchanged.
2. Full-width headroom carving added enough voxel raster work to push an already marginal bake over budget. **Leading:** earlier source `3059c8c119a7...` completed the same bake in 3:57; it already contained the natural supports. The later production delta `83c50f94...` added large full-path-width carve prisms; `54f2088a...` only expanded Y bounds.
3. Unrelated baseline variance alone explains the overrun. This is falsified if reducing only mountain carve volume restores margin while preserving the same region set, vertical clearance, and route semantics.

## Next implementation
Keep the full 3 m visible walking surface, but carve only a centered traversal lane sized from the production motor plus lateral margin instead of clearing the entire path width. Preserve 24-voxel vertical headroom and all support/floor ordering. Add a regression for minimum clear lane width and bounded carve volume; keep primitive count and region footprint unchanged.

## Remaining gates
After source optimization, re-check blast radius/cost. The existing CI ref has already been used once plus its one retry, so do not advance or replace it without an authorized workflow path. Only after green exact-SHA focused CI, exact built-player route/captures, human review, and accepted generated bake/manifest may metadata move open -> pending -> closed, then latest master is merged and the exact feature head is pushed non-force to master.
