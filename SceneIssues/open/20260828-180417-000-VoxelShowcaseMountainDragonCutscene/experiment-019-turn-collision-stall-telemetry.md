# Experiment 019 — Lower-turn collision stall telemetry

## Trigger
Exact feature run `33532092736` tested source `8c7a7b356454fd7cdfb10ee59f1971f8b9038bd2`, which already contains experiment 018's repair aligning `mid-turn` to production resolved point 50 at `(-142.6, 26.6)` m. Focused Mountain Dragon acceptance still passed, but the standalone built-player replay again timed out at waypoint `51/95` immediately after reaching `resolved-49` at approximately `(-140.67, 31.70)`, feetY `22.20`, grounded `True`.

That fires experiment 018's falsifier. Two materially different fixture repairs — precise turn entry and authoritative next-target alignment — have now produced the same acceptance symptom. No further route/tolerance/production behavior change is allowed until the physical stall is isolated.

## Minimal repro
Instrument only the command-line SceneIssue replay path. Once per second while replay is active, log:

- current waypoint index/name and target X/Z;
- production `CharacterMotor.Position` feet X/Y/Z;
- horizontal distance to target;
- feet displacement since the previous sample;
- `CharacterMotor.Grounded`.

The telemetry must not change `AutoWalk`, yaw, speed, gravity, collision, road voxels, waypoint coordinates, arrival radii, or timeout behavior. It exists only to discriminate the repeated built-player stall.

## Competing hypotheses

1. **Hard collision/step barrier:** feet position remains effectively constant while grounded and horizontal distance remains constant.
2. **Off-center sliding/collision deflection:** feet continue moving but distance to the authoritative target does not decrease, indicating the motor is being redirected along a physical boundary.
3. **Grounding/void transition:** grounded state drops or feet Y changes materially while horizontal progress stops.
4. **Replay steering defect:** actor continues to move on ordinary traversable surface in a direction inconsistent with the logged authoritative target despite settled collision/grounding.

## Decision rule
Run this diagnostic source exactly once through `ci-test/fixes/agent-4` after confirming no request is queued/running. Use the built-player log to identify the first stable failure mode. Only then inspect or repair the smallest owning layer demonstrated by the telemetry. If the actor is hard-pinned while grounded, next inspect the realized voxel surface/step at the pinned coordinate before changing route data or shared motor policy.

## Falsifier
If telemetry shows sustained ordinary progress toward `mid-turn` but the replay still reports waypoint 51 timeout, then the assumed movement/collision stall is false; isolate the replay state machine/acceptance predicate before any physical-world repair.
