# Experiment 017 - Turn-entry arrival-radius minimal reproduction

## Why this reproduction exists
Targeted run `33492599541` failed the same standalone SceneIssue replay gate after two materially different repairs. Per `AGENTS.md`, no third fix is allowed until the repeated symptom is reduced to a minimal repro/root cause.

This reproduction uses only the completed run's `WAYPOINT_REPLAY` evidence, the issue-owned route, and the existing replay-harness arrival contract. It does not change production code.

## Observed failure
The replay reached the route point `resolved-49` (route coordinate `(-140.9, 31.5)`) with the player at approximately `(-139.98, 32.31)`, then advanced to `mid-turn` (`(-142.0, 28.0)`). The horizontal miss from the physical player position to `resolved-49` at the moment it was accepted is:

```text
dx = -140.90 - -139.98 = -0.92
dz =   31.50 -   32.31 = -0.81
distance = sqrt(0.92^2 + 0.81^2) ~= 1.23 m
```

The route-wide arrival radius is `1.25 m`, so accepting `resolved-49` at that position is valid under the fixture contract even though the player is still about 1.23 m from the resolved road point.

After advancing, the harness continuously targeted `mid-turn`; the remaining horizontal distance stayed exactly `4.762 m` from roughly 65.2 s until the 100 s timeout while streaming was settled and no runtime exception was emitted. The returned stationary screenshot shows the ordinary player view stopped against the inside terrain at this switchback.

## Root cause
The production road is not being replaced or bypassed: `resolved-49` and `mid-turn` are points from the authoritative resolved production route. The failure is the replay fixture's coarse route-wide arrival tolerance at a constrained turn entry.

At `resolved-49`, the global `1.25 m` tolerance is large enough to advance while the motor remains near the outer/upstream edge. The harness then aims directly at the next resolved point, producing a diagonal chord across the inside of the turn instead of first completing the turn-entry point. Collision remains the production `CharacterMotor` path, so that chord can stop on the carved terrain boundary even though the road centerline itself is traversable.

The shared harness already has the correct reuse boundary: each waypoint may provide its own positive `arrivalRadius`, otherwise it inherits the route-wide radius. Therefore this demonstrated scene-specific constrained turn does not justify a shared API or production-road change.

## Minimal fix constraint
Set only the issue-owned `resolved-49` turn-entry waypoint to a tighter positive arrival radius so the ordinary motor must get close to the authoritative resolved point before the fixture redirects toward `mid-turn`. Keep the route-wide radius unchanged for the rest of the ascent, keep collision/grade/speed policy unchanged, and do not add teleportation or a route shortcut.

A `0.35 m` turn-entry radius is less than one third of the failed early-advance miss and is small relative to the route-wide `1.25 m` tolerance while still leaving normal motor/frame tolerance.

## Falsifier
This root cause is false if an exact-source standalone replay with only the `resolved-49` arrival-radius override still becomes stationary on the same `resolved-49 -> mid-turn` transition. If that happens, do not widen or move the road speculatively; inspect the realized terrain/collision at the exact centerline transition before another fix.
