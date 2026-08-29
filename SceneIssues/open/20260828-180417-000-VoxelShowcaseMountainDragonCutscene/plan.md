# Plan

## Acceptance
Human review requires the exact built VoxelShowcase to show a substantial grounded mountain/readable winding ascent, normal movement from base to summit, a supported dragon placeholder, and proximity dialogue `Hello, I'm Mr. Dragon.` Durable approach/base/switchback/summit/dialogue captures and a source-matched checked-in startup bake are mandatory.

## Proven traversal fix
The prior built replay exposed X/Z-only false arrivals and an actually blocked ascent. The production motor is 0.6 m wide / 1.8 m high with 0.3 m step height. The reusable mountain program orders scenic/support fills -> `Carve` clearance -> restored path floors, and replay requires grounded feet at +4.6 m tiers and +28.0 m summit. Vertical clearance remains 24 voxels / 2.4 m.

## Bake-cost discriminator
Final request `ee738bc85111...` for source `7b5393736485...` failed twice in pre-test baking. Retry logs show ~63 s cold import plus ~177 s actual bake before the 240 s guard. Earlier Mountain Dragon source `3059c8c119a7...` completed the same bake in 3:57.

Hypotheses:
1. Expanded bounds added a region layer — **falsified**; +24 Y voxels stay in the same 512-voxel layers and X/Z are unchanged.
2. Full-width headroom carving was the dominant remaining cost — **falsified as sufficient**; the centered-lane change cut carve volume 46.7%, but exact-SHA run `33274279301` still reached the same external 4-minute process wall after ~62.3 s cold import.
3. Baseline offline presentation scanning is consuming the remaining budget — **supported by source inspection**; `GenerateForBakeBlocking` materializes the full radius-8 semantic world, and every completed terrain/feature region can run `FarFieldStructureStore.CaptureRegion`, which scans coarse columns even though `ShowcaseWorldBake` serializes only semantic region snapshots and `LoadBake` reconstructs far-field presentation afterwards.

## Selected optimization
Keep the exact radius-8 semantic startup image, castle, Kentridge content, mountain program, 24-voxel path clearance, and runtime generation behavior unchanged. During `GenerateForBakeBlocking` only, use an instance-scoped `FarFieldStructureStore.SuppressCapture()` guard. Runtime capture is enabled by default, gallery baking is untouched, and `LoadBake` runs after the scope has disposed and therefore still rebuilds far-field metadata from the accepted semantic snapshots. Change-journal publication stays unchanged because it is constant-time bookkeeping rather than the repeated storage scan. The exact final acceptance filter now includes a regression that proves nested suppression stays disabled until the outer scope exits and restores runtime capture afterwards.

## Remaining gates
Submit one fresh final request from the new exact feature SHA using only `ci-test/fixes/agent-4`; never create another transport or replace queued/running work. Require the source-matched bake/manifest to complete inside the workflow guard, the exact focused acceptance filter green, full grounded built-player route, and human-reviewed captures before open -> pending -> closed. Only then merge latest master and push the exact feature head to master non-force.