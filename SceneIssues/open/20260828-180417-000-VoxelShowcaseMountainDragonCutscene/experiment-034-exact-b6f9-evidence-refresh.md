# Experiment 034 - exact b6f9 replay stale-route and module-validation discriminator

## Exact-source evidence
Targeted run `33957939661` used only `ci-test/fixes/agent-4` and selected feature source `b6f9b2598cc8790c245500b6558938a345d136c1`. The requested current-source bake test passed in 142.992 s. The exported candidate was 13,310,800 bytes with `contentSignature=BE8FDFF3` and SHA-256 `c0f6f5bb0651bac0088b1e12d3a16956816fc62f5fe2dcb83461f9ba7c60cf3`; it is rejected because the player acceptance replay did not complete.

Persistent EditMode tests passed, and the required `VoxelEngine.Structures.Tests.PlayMode` phase passed one test with zero failures. This proves the narrow CI-only CoreRP rendering-debugger guard removed the earlier legacy-input interference without changing Player Settings or production input.

## Traversal discriminator
The production player reached waypoint 32/95 (`resolved-30`) normally, grounded, with no game-side legacy-input exception. At the next checked-in `lower-turn` waypoint it repeatedly reached the requested X/Z around `(-86, 50)` but could never satisfy the anchored vertical band: path-base anchored feet at about 21.70 m while the stale waypoint expected only `+0.8 m`; actual feet at that off-road coordinate were about 25.50 m. The run timed out at 100 s while physically oscillating inside 0.18 m of the stale X/Z target.

The same exact checkout emitted `MOUNTAIN_DRAGON_RESOLVED_ROUTE_DM=` from the production resolver. It now contains 91 points. The stale `lower-turn` coordinate is not on that route; its nearest current point is `(-82.0, 23.0, 52.0)`. The old mid/upper/summit evidence coordinates also predate this current resolved route. Therefore changing CharacterMotor, grade, cut/fill, road policy, or arrival tolerances would be the wrong fix.

## Module-player discriminator
Repository-derived player validation failed only in `CharacterMotorProductionValidation`: it reported 0.71 m of movement. The probe set one heading once and then enabled `VoxelShowcase.AutoWalk`, whose production benchmark deliberately adds 24 degrees/second. That turns the probe off the authored road. The SceneIssue replay in the same build continuously compensates that benchmark turn and had already progressed normally to the stale evidence waypoint, so this failure is validation control logic rather than a demonstrated production motor defect.

## Visual discriminator
`01-mountain-approach.png` and `02-path-base.png` from this run are rejected: bright magenta/purple slab-like semantic proxies dominate the mountain and obscure the readable road entrance. This exact source predates the later `PublishedNearSurfaceCoverage` / semantic-proxy retirement commits ending at `79ac799...`; those later generic far-feature corrections still require exact-player proof.

## Corrective action
1. Regenerate the issue-owned evidence route from the exact 91-point production resolver output: path-base maps to point 0, every subsequent resolved point is traversed in order, semantic lower/mid/upper/summit captures map to current resolved points, and their Y offsets are derived from resolved road Y relative to path-base.
2. Strengthen the evidence regression so the complete fixture and semantic Y offsets must match the current resolved road.
3. Correct only the module-player steering: recompute the heading each frame and compensate the existing 24 degrees/second AutoWalk benchmark turn while still moving through ordinary `VoxelShowcase` + `CharacterMotor` production code.
4. Keep the production road, motor, grade/cut-fill, collision tolerances, summit placement, and Player Settings unchanged.
5. Re-run exact current-head CI after the required master merge and these durable corrections.
