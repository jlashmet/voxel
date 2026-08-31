# Experiment 014 — waypoint replay public seam design

## Problem
`ShowcaseWaypointReplayHarness` currently reflects into private `VoxelShowcase` fields (`_yaw`, `_pitch`, `_mouseLook`, `_motor`) and duplicates the driver's private AutoWalk turn policy as `24f` degrees/second. That makes acceptance replay brittle under ordinary internal refactors and leaks scene-driver implementation details into evidence code.

An independent second consumer, `DeterministicAutoWalkHeadingHarness`, reflects the same private heading/mouse-look state plus private landmark-position behavior and independently duplicates the same `24f` AutoWalk rate. That makes the reuse boundary demonstrated rather than hypothetical: both harnesses should consume one driver-owned semantic automation surface.

## Narrow seam
The smallest semantic public surface is owned by `VoxelShowcase`, not by `CharacterMotor` exposure:
- set a scripted view/heading while suppressing mouse-look input;
- request the heading that should exist *after* the driver's own AutoWalk steering for the next normal movement step;
- read grounded state, feet position, eye height, and horizontal speed for acceptance assertions;
- temporarily select replay sprint movement through a semantic driver control rather than mutating `CharacterMotor.WalkSpeed` from the harness.

The driver must continue to call its existing normal `CharacterMotor.Step`; the seam must not teleport after the initial route start placement or expose the motor object itself. AutoWalk turn compensation should be computed by the driver from its own policy so neither harness duplicates the 24-deg/s implementation constant.

## Current tooling blocker
The connector can perform whole-file UTF-8 replacement and Git blob/tree writes, but the production `VoxelShowcase.cs` is large and current connector reads are truncated. No mounted checkout or partial-patch mutation is available in this worker. Reconstructing and replacing the entire shared scene driver from truncated chunks would create an unacceptable correctness/blast-radius risk for a narrow reuse change.

A separate small-file Git-object path is sufficient for new evidence-only files, and is being used for the accepted-bake handoff, but it does not make the required in-place `VoxelShowcase.cs` seam safe. Keep this requirement open until a complete-file/partial-patch edit path exists; do not move reflection into another helper or expose the private motor as a workaround.

Per SceneIssue rules, record this external/tooling prerequisite and continue independent validated work. Do not weaken the task or mark it complete until the public seam is actually landed and both harness consumers pass through it.
