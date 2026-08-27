# Plan — VoxelShowcase Dirt/grass seam

## Problem
The saved VoxelShowcase camera has two marked Dirt/Moss terrace boundaries. Fresh real-player replay at source `f981771949542628daf1c35b32929d4f0d512b9d` is residency-stable (`missingMax=0`): the lower mark is clean, but the upper mark still contains a rectangular grass tongue.

## Evidence / hypotheses
- Streaming/LOD: falsified by stable replay telemetry.
- Inactive road shoulder authoring: falsified by live catalogue tracing and prior replay.
- District shoulder box steps: real lower-mark cause; reversible ramps removed the metre-scale stairs.
- Stale showcase bake cache: confirmed and fixed by hashing `Assets/Game/WorldBuilder`.
- Full-footprint urban surface correction: confirmed overreach; constrained to built cores.
- Market→upper width jump: confirmed; 2 dm tapered correction cleared the lower mark.
- Upper west midpoint terrain sample: behavioral test passed, but exact replay still showed the upper rectangle; falsified as the final cause.
- Active: saved-camera rays place the surviving upper mark in the civic-summit south-shoulder / upper-shoulder west-edge overlap. Those west envelopes differ by exactly 20 dm, matching the hard plan-view notch. Taper that 20 dm ownership change across the existing 72 dm overlap.

## Minimal fix / regression
Keep terrace geometry and authored district footprints unchanged. In the precedence-16 `upper-shoulder` surface correction, restore only its 72×72 dm west overlap to Moss, then reclaim Dirt in 2 dm bands whose west inset moves from 20 dm to 0. PlayMode regression samples every band and asserts Moss immediately outside/Dirt immediately inside, monotonic ≤1 dm boundary movement, and a bounded 40-primitive correction budget.

## Blast radius / cost
Only the upper-shoulder west overlap from world z 24.0–31.2 m is repainted; no terrain occupancy, roads, structures, other captures, or generic rasterizer code changes. Adds 37 paint primitives to that correction (39 total, budget 40), bake-time only; no per-frame work.

## Verification gate
Issue remains open until one exact-SHA targeted CI request on `ci-test/fixes/agent-8` passes the new PlayMode regression and the exact saved-camera replay. Inspect both marked regions directly; only a clean replay may be promoted to `verification-final.png`.
