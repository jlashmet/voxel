# Experiment 001 — exact-view reproduction on refreshed bake

## Reproduction

Actions run `32843177389` built the ordinary standalone `Assets/Scenes/VoxelShowcase.unity` player from the current committed startup bake and replayed this SceneIssue's exact saved camera. The replay verified the frozen pose and completed successfully.

Evidence:

- artifact `9561358817` — `scene-221508-exact-view`
- digest `sha256:2aecf9b4c7fff5ff35f6ef0df39cf2c03d9cc66a5a868e0d730ad9dd999fa034`

A second diagnostic removed only the circle annotations from a temporary copy of the replay request while preserving the saved camera and pose exactly. Actions run `32843924710` succeeded and produced unobscured evidence:

- artifact `9561637234` — `scene-221508-unobscured-view`
- digest `sha256:d47e47f379a71d4707ff1235d178ba81cdea4f963a77949b112b6eb8d59e88fa`

## Corrected original-versus-current assessment

The original capture has four marked locations.

The first marked replay was too obstructed by its own red annotations to judge the lower seam reliably. The unobscured replay corrects that initial assessment: **the three small lower defects still reproduce**. They lie along one pale/sky-coloured straight strip cutting through the dark street/plaza surface.

Projecting the three saved screen positions through the recorded camera onto plausible horizontal plaza heights gives varying world X but almost identical world Z. At a representative Y=8.0 m plane, the three hits are approximately:

- X=122.34 m, Z=58.91 m
- X=121.84 m, Z=58.93 m
- X=121.36 m, Z=58.95 m

Changing the assumed surface height from roughly 7.0–8.5 m moves the absolute Z together but preserves the same constant-Z relationship. This is strong evidence that all three visible lower marks are one authored world-space boundary around **Z≈590 dm**, not unrelated screen-space cracks.

The large mark is centred on one covered market stall. In the unobscured replay the central timber post visibly seats into its dark stone shoe; the tiny pale vertical openings beside it are views through the open stall/background rather than a post-to-plinth air gap. The marked feature may still involve another beam/post alignment detail, but the local post/shoe contract itself is not broken.

## Market-stall ownership trace

The covered stalls are authored by `KentridgeTownDressingCatalogue.MarketStallProgram`.

The local support geometry already has explicit overlap contracts:

- each stone shoe is 5 dm × 3 dm × 5 dm;
- each timber post is 3 dm × 23 dm × 3 dm;
- the post is inset by 1 dm on X/Z inside its shoe;
- the shoe spans local Y `[0,3)` while the post begins at Y `2`, giving 1 dm of vertical overlap;
- the roof begins at Y=24 while the post reaches Y=25, again overlapping by 1 dm.

Therefore a production edit that merely enlarges the shoe/post would not be supported by the source facts.

## Next diagnostic

Trace the constant-Z lower-town seam through the authoritative town-surface / road / piazza plan. Determine which adjacent authored surfaces own the two sides of the Z≈590 dm join and whether their voxel bounds leave an uncovered row, disagree in elevation, or are geometrically contiguous but rendered with a topology crack.

Only after that ownership boundary is proven should a regression and production fix be written. The lower three circles likely share one causal invariant and should be fixed together.

The large market-stall mark should be reassessed after the broad seam is fixed; it may be a separate issue or may simply have been highlighting the same nearby background gap.

Production attempts used: **0 / 3**.
