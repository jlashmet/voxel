# Experiment 033 — Input-System-only production player blocker

## Observation
Exact-SHA run `33947319899` used source `1221be0f1b1bc36645ff149a27836c01802556e5`. The requested `MountainDragonRoadPresentationTests.ResolvedSpiralNeverCutsDeeperThanItsOpenSkyClearance` passed, so the current resolved Mountain Dragon centreline does **not** exceed the existing 24 dm corridor clear-above contract. The earlier hypothesis that >24 dm centreline cuts were leaving the observed mountain overhang is therefore falsified for this source.

The same checkout then built and launched production `VoxelShowcase`, but `player-run.log` repeatedly emitted:

`InvalidOperationException: You are trying to read Input using the UnityEngine.Input class, but you have switched active Input handling to Input System package in Player Settings.`

The stack is `UnityEngine.Input.GetKeyDown -> VoxelEngine.Showcase.VoxelShowcase.HandleKeys -> VoxelEngine.Showcase.VoxelShowcase.Update`. Replay diagnostics remained at waypoint 0 with `grounded=False`; no base-to-summit traversal occurred. The workflow process-level standalone step reporting success is therefore not acceptance evidence: this SceneIssue explicitly requires no runtime exceptions and normal grounded traversal.

## Root cause
`VoxelShowcase` still uses the legacy-shaped `Input` surface across the entire interactive frame path: keys, mouse look/scroll, movement/sprint/jump, mouse buttons, and `ResetInputAxes`. Current master configures the player for Input System-only handling. Fixing only the first `HandleKeys` call would move the exception to the next legacy read in the same frame.

The repository already centralizes physical device ownership in `Game.Input.Runtime`; `UnityPlayerInputReader` uses Input System. The narrow reusable correction is therefore an Input-System-backed compatibility adapter in that runtime layer plus a Showcase forwarding facade that preserves the existing driver call shape. Do not switch global Player Settings back to legacy/both input and do not put direct device polling into Mountain Dragon composition.

## Required proof
1. Exact current feature SHA compiles the Showcase and Input runtime modules with the compatibility adapter.
2. Production `VoxelShowcase` standalone replay emits no legacy-input exception.
3. Replay progresses through the authoritative Mountain Dragon route with normal grounded movement; process exit alone is insufficient.
4. Re-review fresh screenshots after traversal becomes valid. The current two stationary captures remain visually rejected and cannot establish final mountain/path acceptance.
