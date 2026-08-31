# Experiment 014 — waypoint replay public seam design

## Problem
`ShowcaseWaypointReplayHarness` currently reflects into private `VoxelShowcase` fields (`_yaw`, `_pitch`, `_mouseLook`, `_motor`) and duplicates the driver's private AutoWalk turn policy as `24f` degrees/second. That makes acceptance replay brittle under ordinary internal refactors and leaks scene-driver implementation details into evidence code.

## Narrow seam
The smallest semantic public surface is owned by `VoxelShowcase`, not by `CharacterMotor` exposure:
- set a scripted view/heading while suppressing mouse-look input;
- request the heading that should exist *after* the driver's own AutoWalk steering for the next normal movement step;
- read grounded state, feet position, eye height, and horizontal speed for acceptance assertions.

The driver must continue to call its existing normal `CharacterMotor.Step`; the seam must not teleport after the initial route start placement or expose the motor object itself. AutoWalk turn compensation should be computed by the driver from its own policy so the harness never duplicates the 24-deg/s implementation constant.

## Tooling blocker
The GitHub connector available to this worker has whole-file create/update operations but no patch operation. `VoxelShowcase.cs` is a large shared scene driver; implementing the seam requires a small edit in that file, but a connector-side whole-file replacement creates unacceptable blast radius compared with the assignment's narrow reuse requirement. The exact blob was fetched successfully, so the design is not blocked on source understanding; it is blocked on a safe narrow write mechanism/local checkout.

Per SceneIssue rules, record this external/tooling prerequisite and continue independent validated work. Do not weaken the task or mark it complete until the public seam is actually landed and replay passes through it.
