# Experiment 003 — fresh-bake verification of positive-edge fix

## Source

- Scene issue: `20260824-221508-896-VoxelShowcase`
- Production fix commit: `971cf8371d95be29fff59675d2c31c2f4d94af65`
- Verification workflow commit: `6df4a6f3a59262975c6a39bb8a8f01866818b84a`
- Verification run: `32853747303`
- Production-fix attempt: 1 / 3

## Hypothesis

The visible seam is caused entirely by both Market Square voxel layers omitting the authored positive X/Z endpoint. Extending the graded square and hard piazza from count `SizeDm * scale` to the inclusive-span count `SizeDm * scale + 1` should remove the exposed Z≈590 dm row and the nearby exposure around the stall foot.

## What was performed

1. Changed only the hard-piazza and graded-square width/depth counts to include the positive authored endpoint; placement, height, material, and precedence were unchanged.
2. Re-ran `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests.HardAndGradedPiazzaOwnTheSameInclusiveAuthoredBoundary`; it passed after having failed red on the baseline.
3. Ran the full `VoxelEngine.Tests.EditMode.KentridgeMarketPiazzaTests` class; it passed.
4. On run `32853747303`, regenerated `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes` from the source fix before building the standalone player. The bake completed successfully (`199 regions`, `10.6 MiB`).
5. Replayed the saved scene-issue camera in the standalone player with annotations removed. `player-run.log` reported `Verified standalone frozen pose.` The workflow uploaded the fresh bake and `replay-latest.png` as `scene-221508-unobscured-view`.

## Result

Structural regression: **pass**.

Fresh exact-view visual verification: **fail**. The regenerated scene still shows the broad light-blue straight seam through all three lower marked regions, and light-blue exposure remains adjacent to the central market-stall foot. The seam is visually still the reported defect, not merely a stale committed bake.

The fresh frame is preserved in workflow artifact `scene-221508-unobscured-view` from run `32853747303`; the local inspection copy came from that artifact, not from the earlier pre-fix replay.

## What was learned

**Hypothesis disproven as a complete cause.** The plaza endpoint mismatch was real—the red regression proved it—and correcting it is a valid boundary-contract improvement, but it is not sufficient to remove the captured scene defect. The visible line therefore comes from another ownership/raster/material boundary that happens to align with the Market Square positive-Z area, or from a higher-level/later stage that overrides or exposes that row.

Production attempt 1 is unsuccessful for the SceneIssue and counts as 1 / 3.

## Next

Do not enlarge the piazza again or patch the screenshot coordinates. Trace the actual light-blue pixels/voxels at the saved seam after the fresh bake: determine whether they are empty/background, a specific material, a road/plaza precedence overwrite, a mesh boundary, or a neighboring generated feature. The next production attempt must target that proven owner.
