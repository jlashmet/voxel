# Plan — semantic tree interaction

## Repro / affected region
The report applies to the visible Showcase trees; the capture has no separate circle annotations. Experiment 001 reproduced the player passing through the affected tree because movement only consulted voxel/physics collision. Exact saved-camera replay in experiment 017 no longer contains the capture-era north-field tree geometry under the current authoritative bake; the replay reports `treeCount=36`, so that visual change is bake drift rather than absence of the semantic tree runtime.

## Hypotheses and discriminators
1. Add ordinary/generated feature colliders. Rejected as the owning model: generated vegetation is semantic voxel data and persistent collider rebuilding would broaden lifecycle/cost.
2. Reuse feature/projectile collision for movement. Rejected by experiment 002 because canopy leaves would become solid.
3. Query surviving semantic Wood for movement and use the same tree state for damage. Supported: production-path tests show Wood blocks a player-sized probe, Leaves/Air do not; branch/trunk hits mutate tree state and emit Wood/Sapling/Leaves drops; motor/projectile integration routes through semantic state before legacy chunk mutation.
4. Saved-view trees still missing after the fix. Experiments 009/012/017 falsify this as an interaction bug: the authoritative startup bake changed after capture.

## Fix and regression
`VoxelShowcase`/`CharacterMotor` consult semantic tree blocking after voxel collision checks; removed branches cease blocking immediately while foliage remains traversable. Projectile sweeps hit semantic tree state before legacy chunk mutation. `ShowcaseTreeInteractionRegressionTests` covers player-sized Wood vs Leaves, branch severing/trunk felling/drops/fallback IDs, and motor/projectile integration.

## Blast radius / cost / verification
Showcase-scoped; generic voxel collision is unchanged. Movement performs a fixed 8×3 = 24 semantic probes per sweep plus the grounded semantic check; no collider rebuild/cache is introduced. Final targeted CI request `c73d05c39be8138247dbdfa1ff5d31447faaf59f`, based directly on feature SHA `82d6a19c285e28f8475b24f1dd293a73b58ef0f3`, passed run `33026770303`: 3 cases, 72 s, peak 5152 MB (limits 240 s / 14336 MB). `verification-final.png` is retained from the successful saved-pose runtime replay in run `32999019598`; its sky/fog-only view is expected because the current bake no longer contains those capture-era trees.
