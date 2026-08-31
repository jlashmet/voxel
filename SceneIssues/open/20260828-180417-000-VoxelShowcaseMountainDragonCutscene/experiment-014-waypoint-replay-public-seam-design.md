# Experiment 014 — waypoint replay public seam design

## Problem
`ShowcaseWaypointReplayHarness` currently reflects into private `VoxelShowcase` fields (`_yaw`, `_pitch`, `_mouseLook`, `_motor`) and duplicates the driver's private AutoWalk turn policy as `24f` degrees/second. That makes acceptance replay brittle under ordinary internal refactors and leaks scene-driver implementation details into evidence code.

A second independent production harness, `DeterministicAutoWalkHeadingHarness`, has the same problem: it reflects `_yaw` / `_mouseLook` (and private `LandmarkWorldPosition`) and duplicates the same `24f` AutoWalk turn rate. This is useful reuse evidence: the correct seam should serve both harnesses rather than being shaped only around the Mountain Dragon route.

## Narrow seam
The smallest semantic public surface is owned by `VoxelShowcase`, not by `CharacterMotor` exposure:
- set a scripted view/heading while suppressing mouse-look input;
- request the heading that should exist *after* the driver's own AutoWalk steering for the next normal movement step;
- read grounded state, feet position, eye height, horizontal speed, and the driver's normal/sprint movement semantics needed by evidence replay;
- expose landmark position semantically if the deterministic circular AutoWalk harness still needs it, rather than reflecting a private method.

The driver must continue to call its existing normal `CharacterMotor.Step`; the seam must not teleport after the initial route start placement or expose the motor object itself. AutoWalk turn compensation should be computed by the driver from its own policy so neither harness duplicates the 24-deg/s implementation constant. The second harness is the independent consumer that proves the seam survives ordinary `VoxelShowcase` internal refactors.

## Read-only implementation discriminator
Current source confirms the coupling is exactly the one described above:
- `ShowcaseWaypointReplayHarness` binds `_yaw`, `_pitch`, `_mouseLook`, and `_motor` with `BindingFlags.NonPublic`, reads `CharacterMotor.Position/Grounded`, mutates `WalkSpeed`, and pre-compensates by `24f * Time.deltaTime` before setting `AutoWalk = true`.
- `DeterministicAutoWalkHeadingHarness` independently reflects `_yaw`, `_mouseLook`, and private `LandmarkWorldPosition`, then performs the same `24f * Time.deltaTime` pre-compensation.
- `VoxelShowcase.StepAutoWalk` is the sole owner of the real `24f` policy and `MovePlayer` subsequently builds the ordinary forward wish vector and calls `_motor.Step(...)`.

Therefore the acceptance-preserving change is small in design: expose semantic scripted-control/observation methods on `VoxelShowcase`, consume them from both harnesses, and leave `MovePlayer` / `CharacterMotor.Step` unchanged.

## Tooling blocker
The currently available GitHub connector can create blobs/trees and can replace UTF-8 files, but its existing-file write action requires the complete replacement contents. Whole-file reads of `VoxelShowcase.cs` (about 58 KB) are truncated by the connector response budget; there is no partial-patch write action and the repository is not mounted in the local container. Fetching disjoint line ranges is sufficient for source understanding but reconstructing a production driver from those chunks would create an unacceptable risk of omission/line-boundary damage for a change whose required blast radius is only a few methods.

The earlier assumption that blob/tree support alone cleared the blocker was therefore incorrect. Blob/tree writes still require a complete new blob; they do not provide patch application.

Per SceneIssue rules, keep this external tooling prerequisite explicit and continue independent work. Do not move the reflection into another helper, expose `CharacterMotor` directly, weaken the reuse task, or mark it complete until a safe narrow edit lands and both consumers pass through the public seam.
