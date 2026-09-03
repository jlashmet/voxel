# Experiment 024 - current route discriminator result

## Exact-source evidence
Completed targeted run `33719954172` on the only allowed `ci-test/fixes/agent-4` transport. The automatically selected `Game.Composition.Showcase.Tests.EditMode` assembly executed the module-local serializer successfully before the unrelated rendering failures and emitted the current production route.

The authoritative route contains 96 resolved points. Its terminal sequence is:

- 88 `(-1042,457,297)` dm
- 89 `(-1080,468,280)` dm
- 90 `(-1089,471,288)` dm
- 91 `(-1120,482,260)` dm
- 92 `(-1119,483,256)` dm
- 93 `(-1120,493,220)` dm
- 94 `(-1120,495,200)` dm
- 95 `(-1089,495,183)` dm

The same assembly also emitted a fresh startup bake payload with manifest `contentSignature=7554A9C4` and SHA-256 `44cb5af102a90ce84d9d51e9a40f9a5bf779bc9d1ad881fe9a04fd1a2d825632`.

## Discriminator result
The checked-in evidence route is stale after the summit-supported point: its old terminal sequence uses `(-1100,*,260)` / `(-1100,*,200)` and omits the new intermediate resolved points. That part of hypothesis 1 is proven.

However, the repeated built-player hard stop is at resolved point 89, and current authoritative point 89 remains exactly `(-1080,468,280)` dm. Therefore stale terminal evidence does **not** explain the collision stop. The failing segment itself survives in the current route, so hypothesis 2 is also true for the acceptance symptom: the next product discriminator must isolate the realized terrain-corridor/collision mismatch around resolved points 88-91 before any further composition or traversal fix.

## CI blocker
The overall run failed in `VoxelEngine.Rendering.Tests.EditMode` with the known unrelated GPU renderer regressions. Standalone SceneIssue replay was skipped because automatic module validation failed first. This remains an external blocker for exact built-player acceptance; no renderer files are changed by this assignment.

## Decision
- Correct/regenerate the stale evidence fixture only from current authoritative resolver output; do not treat that bookkeeping correction as the traversal fix.
- Freeze further route-control, motor, tolerance, grade, cut/fill, or summit-placement changes until a minimal realized-corridor/collision repro distinguishes the obstruction source around current points 88-91.
