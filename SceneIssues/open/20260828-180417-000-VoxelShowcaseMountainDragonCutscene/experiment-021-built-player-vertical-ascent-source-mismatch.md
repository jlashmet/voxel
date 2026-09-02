# Experiment 021 — Built-player vertical ascent source mismatch

## Trigger
Exact feature run `33653746253` tested source `cccfbd858bb60bea6b95d763c479712e697dcee8`. Experiment 020 succeeded: the standalone player emitted `WAYPOINT_REPLAY diagnostic activated` and periodic motor samples. The replay again stopped advancing after grounded `resolved-49`, but telemetry disproves a physical movement stall.

## Observed built-player behavior
The route established `path-base` as the vertical anchor at feet Y `21.60` m. At `mid-turn` (authoritative resolved point 50, target `(-142.6, 26.6)` m), the production motor repeatedly reached within centimetres of the target while staying grounded:

- horizontal distance repeatedly fell below `0.15` m, including samples near `0.005` m;
- feet Y stayed approximately `22.10` m;
- grounded stayed `True`;
- movement continued around the target rather than pinning on a collider.

The waypoint intentionally requires `expectedYOffset: 5.0` with `yTolerance: 1.0`, so acceptance requires feet Y near `26.60` m relative to the `21.60` m base anchor. The built road is only about `+0.50` m above the base there. The replay therefore reaches the waypoint horizontally but correctly refuses to certify the required ascent.

## Discriminated hypotheses
1. **Hard collision / step barrier.** Rejected: the motor repeatedly reaches the target horizontally, grounded.
2. **Boundary deflection.** Rejected: distance reaches centimetres and oscillates around the target.
3. **Grounding or void transition.** Rejected: grounded remains true and feet Y is stable.
4. **Replay steering/state-machine defect.** Rejected for the repeated symptom: horizontal arrival succeeds; the remaining failed predicate is vertical ascent.
5. **Current production mountain/road legitimately remains flat here.** Not supported. `mid-turn` lies roughly 32 m from the authored mountain core centre. The current 28 m-high analytic core should already rise several metres there, and the road's shared maximum cut/fill is only 42 dm (4.2 m), so a built surface only ~0.5 m above base cannot represent the current mountain/road contract.
6. **Standalone player is consuming source-mismatched world bytes.** Leading hypothesis. `showcase-player-capture.sh` builds the current scene but does not regenerate the checked-in VoxelShowcase startup world payload. The assignment already records that tracked `ShowcaseWorld.bytes` is stale and lacks a source-matching provenance manifest.

## CI-side independent failure
The same exact run failed automatic planning before Unity module validation because the assignment still carried the obsolete convention registration `Assets/Game/Composition/Showcase/Tests/mountain-dragon.module-validation.json`. Current merged CI explicitly rejects changed `*.module-validation.json`; module ownership is asmdef/convention-derived. Delete only that stale issue-owned registration rather than weakening the planner.

## Decision rule
Do not relax `expectedYOffset`, y tolerance, route coordinates, motor policy, road grade, or cut/fill. First restore/enforce startup-bake source provenance and obtain a fresh current-source VoxelShowcase payload through the sanctioned bake path. Then replay the exact route. If a fresh source-matched payload still reports ~22.1 m at `mid-turn`, the stale-payload hypothesis is false and the current physical road realization becomes the owning defect to inspect.
