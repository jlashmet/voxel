# Experiment 017 — current-bake exact saved-view replay

## Hypothesis
The trees reported in the original capture belonged to the stale capture-era startup bake and are absent from the current checked-in `VoxelShowcase` bake; therefore the exact saved camera should no longer show those orphan tree visuals even though the current scene still publishes its normal semantic tree population.

## What was performed
- Diagnostic source commit: `4f785189292f98f9f1e449397503f17a3b3b48f2` (feature differs only by later experiment documentation).
- CI request commit: `937217546cf0b76bf284b673ef6c19b765598089`; run `32999019598`.
- Ran exactly `VoxelEngine.CI.SceneIssue20260825033053TreeInteractionTests.CapturedViewTreeBlocksPlayerAndRespondsToShot` in PlayMode.
- The fixture loaded `VoxelShowcase`, pinned `Showcase Camera` to the saved position/rotation/FOV, settled 60 frames, and wrote `verification-current-replay.png` plus metrics *before* the semantic-tree visibility assertion.
- Artifact `single-test-32999019598` / ID `9617959964`, digest `sha256:bfb19402e94e4cc7eb3b38553ca74324919a33b8e9e441ca6e22d47f0583385b` uploaded successfully.

## Result
The current replay frame at the exact saved camera contains sky/fog only; **no tree geometry is visible**. The metrics report `semanticTreeCount=36`, confirming the ordinary semantic tree population still exists elsewhere in the scene. The diagnostic test then failed after executing exactly one case with `No authored semantic branch geometry is visible and shootable from the saved camera view.`

The text provenance is saved beside this experiment as `verification-current-replay.txt`. The exact PNG remains in the cited immutable CI artifact because the connector path used for repository bookkeeping is text-only for file creation.

## What was learned
**Hypothesis confirmed.** The tree visuals implicated by the 2026-08-25 03:30Z capture are not present in the current authoritative saved view after the later startup-bake refresh. Combined with Experiment 012's bake-blob change, this isolates the capture-specific shooting symptom to stale baked geometry rather than a currently rendered non-semantic tree population. Separately, the player-collision regression was real: semantic trees existed but `CharacterMotor` queried only voxel storage; the production fix adds surviving-wood semantic collision while the pre-existing projectile path continues to damage semantic tree branches.

## Next
Remove the temporary capture-specific CI replay test, keep the permanent `ShowcaseTreeInteractionRegressionTests`, update the plan, and run that permanent three-case regression through targeted CI from the exact final production/test feature head. Only after it is green should terminal issue bookkeeping be written.
