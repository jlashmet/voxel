# Experiment 018 — Mid-turn resolved-road drift minimal repro

## Trigger
Exact feature run `33510365863` tested source `1e91acf7bcae2cb01b7d7db6a04d8f37c204d334`. The focused Mountain Dragon production acceptance passed, but the standalone built-player replay timed out immediately after reaching `resolved-49` even with experiment 017's 0.35 m arrival radius.

This fires experiment 017's falsifier: do not relax tolerance again; inspect the realized turn geometry/collision and the next target.

## Observed replay
The built player reached:

- `resolved-48` at approximately `(-139.22, 32.98)`;
- `resolved-49` at approximately `(-140.67, 31.70)`, feetY `22.20`, grounded `True`;
- then remained unable to reach the next `mid-turn` waypoint until the 100 s replay timeout.

Streaming was settled (`missingVisible=0`) and frame rate remained high, so this was not a loading/performance stall.

## Minimal geometry discriminator
The production route is a 30 dm (3.0 m) carriageway. The generated evidence fixture intentionally substitutes named capture waypoints for selected resolved points.

At this switchback the fixture had:

- production `resolved-49`: `(-140.9, 31.5)` m;
- hand-authored `mid-turn`: `(-142.0, 28.0)` m;
- production resolved point 50: `(-142.6, 26.6)` m.

The hand-authored `mid-turn` is about `sqrt(0.6^2 + 1.4^2) = 1.52 m` from resolved point 50. That is already beyond the nominal 1.50 m half-width of the carriageway before accounting for the steep switchback cut/fill edge. The replay therefore redirects the actor from an exact production-road point toward a fixture point just outside the production centerline corridor, where ordinary `CharacterMotor` collision stops progress.

This also explains why experiment 017's precision repair could not succeed: `resolved-49` is now reached precisely, but the following target itself is off the authoritative road.

## Competing hypotheses

1. **`resolved-49` is still accepted too early.** Rejected: run `33510365863` reaches it within the 0.35 m override before the stall.
2. **Streaming/render load stalls movement.** Rejected: `missingVisible=0`, no exception, and high frame rate persist during the timeout.
3. **Shared road or motor policy is invalid.** Not supported: focused production acceptance passes and every preceding resolved point is traversed grounded under the same road/motor contracts.
4. **The named capture waypoint drifted off the generated production road.** Supported: `mid-turn` differs from resolved point 50 by ~1.52 m and is the first unreachable target.

## Minimal repair
Keep production road geometry, grade, cut/fill, motor collision, speed, route-wide arrival radius, and experiment 017's precise `resolved-49` entry unchanged. Move only the issue-owned `mid-turn` capture waypoint onto authoritative resolved point 50 at `(-142.6, 26.6)` m.

Add a regression that loads the issue evidence route and proves each named ascent capture waypoint (`lower-turn`, `mid-turn`, `upper-turn`, `summit-supported`, `summit-proximity`) exactly matches the production resolved point it substitutes. This prevents future evidence-fixture drift without adding scene policy to shared systems.

## Falsifier
If the exact-source built-player replay still stalls at `resolved-49 -> mid-turn` after `mid-turn` is aligned to resolved point 50, the target-location hypothesis is false. Inspect the realized voxel surface/collision at resolved point 50 and the motor's per-frame position/grounding before any further route or tolerance change.
