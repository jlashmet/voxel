# Experiment 003 — Rossdam lake route conflict

## Hypotheses
1. Rossdam Lake is oversized/misplaced and accidentally blocks an unrelated verified route.
2. The substantial modern lake genuinely intersects the verified Bandit Hideout travel corridor and therefore needs the same explicit semantic route-around treatment required by the feature contract.

## Action / source
- Exact tested source: `c447467b897b430cdc335582a33f0fc6b1dca526` via request `849f93f0b838b77b07fa1d24529f9fd69fa44dd2`.
- CI run: `33232755172`.
- Mapped the authoritative coarse graph: Fighting Area I `(0,3)`, Fighting Area II `(0,4)`, Bandit Hideout `(-2,4)`, Moordell Corridor `(1,3)`, Rossdam Approach `(-1,4)`.
- Rossdam Lake is a substantial `Between(MoordellCorridor, RossdamApproach)` region centered near `(-0.375,3.5)` coarse cells after its west offset. Its footprint reaches the north spine and the straight Fighting Area I -> Bandit Hideout corridor grazes its western shore.

## Result
The focused PlayMode run failed because `fighting-area-1->bandit-hideout` entered `rossdam-lake` without an authored solution. The built `KentridgePlayableSlice` hit the same planner exception during `OnEnable`, so no usable voxel surface or macro evidence sequence was produced (`visible=0`; diagnostic screenshots only).

## Verdict
Hypothesis 1 is rejected as the primary repair: shrinking the lake enough to clear the bandit spur would weaken the intended substantial lake and its explicit effect on the northern/Rossdam routes. The verified topology remains unchanged; modern physical geography needs an explicit dry shoreline solution for the bandit spur. This is not claimed as legacy geography evidence.

## Next step
Author `FightingArea1 -> BanditHideout` / `RossdamLake` as `GoAround` with the same dry-road clearance, and regress that the route is geography-constrained while every corridor tile stays outside the water footprint.
