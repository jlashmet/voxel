# Experiment 001 — exact-view reproduction on refreshed bake

## Reproduction

Actions run `32843177389` built the ordinary standalone `Assets/Scenes/VoxelShowcase.unity` player from the current committed startup bake and replayed this SceneIssue's exact saved camera. The replay verified the frozen pose and completed successfully.

Evidence:

- artifact `9561358817` — `scene-221508-exact-view`
- digest `sha256:2aecf9b4c7fff5ff35f6ef0df39cf2c03d9cc66a5a868e0d730ad9dd999fa034`

## Original versus current view

The original capture has four marked locations.

Three small lower marks lie along one conspicuous light/sky-coloured diagonal crack through the dark street surface. In the current exact replay those normalized locations land on a continuous road/piazza edge; the original street crack no longer reproduces. This is consistent with the refreshed Kentridge startup world committed while resolving the preceding SceneIssue.

The large mark is centred on one of the covered market stalls, around a timber support and its stone shoe/plinth. The current replay's red annotation overlaps most of this join, so visual inspection alone cannot yet establish whether a remaining physical gap exists.

## Ownership trace

The covered stalls are authored by `KentridgeTownDressingCatalogue.MarketStallProgram`.

The local support geometry already has an explicit overlap contract:

- each stone shoe is 5 dm × 3 dm × 5 dm;
- each timber post is 3 dm × 23 dm × 3 dm;
- the post is inset by 1 dm on X/Z inside its shoe;
- the shoe spans local Y `[0,3)` while the post begins at Y `2`, giving 1 dm of vertical overlap.

Therefore there is no authored post-to-shoe air gap in the market-stall program. A production edit that merely enlarges one of those primitives would be unsupported by the source facts and risks hiding a rendering/annotation/perspective issue.

## Next diagnostic

Replay the exact same camera with `circles` removed from a temporary copy of `issue.json` in the CI checkout only. The camera/pose remains unchanged; only the red overlay is suppressed. Inspect the market-stall join unobscured.

If a real slit remains despite the source overlap, trace below the feature program into placement/rasterisation/rendering. If the join is visually continuous, treat the large mark as already resolved and close this SceneIssue based on current committed world state rather than making a speculative production edit.

Production attempts used: **0 / 3**.
