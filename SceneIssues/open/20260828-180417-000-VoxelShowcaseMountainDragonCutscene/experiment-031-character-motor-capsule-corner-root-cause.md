# Experiment 031 — character motor capsule corner root cause

## Purpose

Isolate the repeated late upper-approach stall through the production `CharacterMotor.IsBlocked` path before making another traversal-policy change.

## Exact evidence

Targeted CI run `33874459381` used source SHA `4e1de8ec04c585856ad09b7d8c025c1eaed12fdb` and requested `MountainDragonCharacterMotorBlockerDiagnosticTests.UpperApproachRaisedNegativeXSweepSerializesProductionBlocker`.

The requested discriminator passed in 21.93s and serialized the exact raised negative-X probe:

- feet: `(-104.640, 45.900, 28.000)` metres
- capsule radius: `0.300` metres
- broad voxel bounds: `[-1050,459,277..-1044,476,282]`
- authoritative blocker cells: `(-1050,459,282)` and `(-1050,460,282)`, both material `13` (road/dirt)
- vegetation discriminator: false; blocker is authoritative voxel occupancy

The offending voxel column occupies X `[-105.0,-104.9]` and Z `[28.2,28.3]`. Its closest horizontal point to the player centre is `(-104.9,28.2)`, about `0.328m` away, outside the `0.300m` capsule radius. It was counted only because production voxel collision used the enclosing square AABB as the final footprint test.

The workflow as a whole failed later in automatic module validation and therefore skipped standalone replay; that does not invalidate the requested discriminator result. No queued/running CI was replaced.

## Conclusion

Root cause is a reusable `CharacterMotor` collision-boundary defect, not mountain route policy, vegetation, summit placeholder overlap, or another road-resolver failure. The broad AABB remains useful for candidate enumeration, but each candidate voxel column must overlap the circular horizontal capsule footprint before it can block movement.

Selected correction:

1. filter authoritative voxel collision through circle-vs-voxel-cell overlap;
2. use the same circular footprint when `SnapToGround` chooses supporting columns;
3. preserve half-open/tangent contact semantics and the existing semantic-tree collision authority;
4. regress both the exact Mountain Dragon production-world corner case and independent capsule/voxel geometry.

Implementation commits: `b3f92a8ea2d83011a0f8d9d20aa8f3dbff1b5d5d`, `ab65ee5de2a5585e20cdb96970ecae785064d2ad`, `c6d8151c1a0123058ead732ac210a08a77de2c31`, `fcc55755d6d351b608214c483be6aced4c43968a`.
