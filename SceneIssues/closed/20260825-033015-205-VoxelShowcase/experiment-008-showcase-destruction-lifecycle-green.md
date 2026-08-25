# Experiment 008 — Showcase destruction lifecycle verification

## Hypothesis
Retiring the rooted presentation on a structural trunk sever will preserve the existing branch-debris lifecycle while making the remaining whole tree visible as one falling body that topples and expires.

## What was performed
Source commit: `eaead8ede86cbf90e36ead8d92ddbc4a34083aa9`.

Ran `VoxelEngine.CI.ShowcaseTreeDestructionVisualTests.ShowcaseTornado_BreaksBranchAndSeversTreeVisibly` through `ci/single-test` on request commit `7a70a7439fc09c413bf6ffbf3bc0074ec30acab1` (workflow `32896212772`).

## Result
The workflow passed and executed exactly one test. The upper branch detached visibly (`2172` pixels), traveled `1.530 m`, rotated `57.40°`, and expired. The later structural cut was trunk branch `1`; `severedAfterTrunk=True`, `rootedPresentationAfterTrunk=False`, one break cap was present, the whole-tree body contributed `89986` visible pixels, traveled `0.704 m`, tilted `100.41°`, remained visible while toppling (`89551` pixels), and expired. No tornado remained active at the end.

## What was learned
**Hypothesis confirmed.** The new structural-sever ownership does not regress ordinary connected-branch destruction and provides a visible, finite whole-tree fall while leaving no rooted procedural presentation.

## Next
Run the batching/query regression that covers batch release, rooted-topology retirement, and post-sever collision queries.
