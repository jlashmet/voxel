# Experiment 030 — upper-approach CharacterMotor blocker

## Trigger

Exact current-source bake/replay run `33839531278` reached 89/95 route waypoints, then stalled grounded at feet approximately `(-104.590,45.600,28.000)` m while targeting `resolved-89` at `(-108.0,28.0)` m X/Z. Movement remained zero until timeout. Built-player telemetry reported the intended X sweep as `voxel:true/wood:false`; the orthogonal/current and raised positions were clear, while the same X sweep remained blocked after the normal 0.3 m step-up.

The same run's screenshots were not visually accepted: the mountain still read as segmented/exposed masses and the upper road had abrupt terrain faces.

## Falsified follow-up

Commit `5b1a3088...` attempted to make semantic resolver vertices individually step-height-safe by lowering scene grade to 140 permille and raising cut/fill to 64 dm. Exact request `6b0f48e...` failed deterministically because the route required 70 dm cut/fill. A second attempt restored 280/42 and changed resolver spacing to 10 dm. Run `33857362837` then showed adjacent `ResolvedWorldRoad.Points` may still differ by 19 dm across a legal 70 dm planar run.

`WorldRoadResolver.TryRouteLeg` removes collinear grid samples before `Grade`; therefore `ResolvedWorldRoad.Points` is a sparse semantic polyline, not the physical character-step surface. The 3 dm-per-resolved-point assertion was invalid and is removed. Production layout returns to the last source-matched 20 dm / 280 permille / 42 dm configuration from `138dd29a...`.

## Minimal discriminator

`CurrentProductionUpperApproachCapsuleSerializesBlockingVoxelForCollisionIsolation` recreates the exact recorded feet position in the real production `ShowcaseWorld`, invokes the shipped private `CharacterMotor.FootMin`, `FootMax`, and `IsBlocked` collision seams, reproduces the intended negative-X half-voxel sweep and grounded step-up attempt, and serializes every authoritative occupied voxel/material inside those exact AABBs.

It changes no route, motor tolerance, terrain, road rasterization, vegetation, summit, placeholder, or global budget policy.

## Decision rule

If the blocker is road/terrain-owned, repair the physical corridor/terrain realization boundary and add an independent regression there. If it belongs to another scene-composed feature, keep the correction in Showcase composition. Do not request another full bake/replay until this exact blocker identifies the owning system.
