# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The prior built replay exposed X/Z-only false arrivals and an actually blocked ascent. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height. The reusable mountain program orders scenic/support fills -> `Carve` clearance -> restored path floors, and replay requires grounded feet at +4.6 m tiers and +28.0 m summit. Vertical clearance remains 24 voxels / 2.4 m.

## Bake-cost discriminator
Final request `ee738bc85111...` for source `7b5393736485...` failed twice in pre-test baking. Retry logs show ~63 s cold import plus ~177 s actual bake before the 240 s guard. Earlier Mountain Dragon source `3059c8c119a7...` completed the same bake in 3:57.

Hypotheses:
1. Expanded bounds added a region layer — **falsified**; +24 Y voxels stay in the same 512-voxel layers and X/Z are unchanged.
2. Full-width headroom carving pushed the marginal bake over budget — **supported**; natural supports were already in the 3:57 candidate, while `83c50f94...` later added the large carve prisms.
3. Baseline variance alone explains the overrun — still falsifiable only by exact-SHA timing of the optimized source.

## Selected optimization
Current candidate `e71a165aa721...` keeps the full 30-voxel / 3 m visible path and 24-voxel vertical headroom, but carves a centered 16-voxel / 1.6 m traversal lane. That leaves 0.5 m lateral margin on each side of the 0.6 m motor. Region footprint and primitive count remain 1200 x 306 x 1200 and 76; carve volume falls 5,097,000 -> 2,718,400 voxels (-46.7%). A focused production-program regression enforces lane width, carve count/volume, tapered support, and shared budgets. No steady-state runtime work was added.

## Remaining gates
The CI ref was already advanced once and its one retry consumed, so do not update or replace it without an authorized workflow path. Require green exact-SHA focused CI, source-matched bake/manifest, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Only then merge latest master and push the exact feature head to master non-force.
