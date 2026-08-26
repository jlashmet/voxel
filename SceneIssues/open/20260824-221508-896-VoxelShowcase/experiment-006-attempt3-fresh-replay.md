# Experiment 006 — attempt-3 fresh exact-pose replay

## Hypothesis

If zero-thickness authored interfaces were the remaining visual cause, attempt 3 should remove the blue exposure both at the market-stall support and along the three lower marked regions after a fresh VoxelShowcase bake.

## What was performed

Production fix under test: `9c839dcbbe73bb3f325db8d3dd3ef380d22343cf`.
Durable feature state used for replay: `064745e010ad8fb9ecac1f955969835eb14dd954`.
CI-only replay commit: `745cf785217a917cfc0b12deb049975584253f0b`.
Workflow run: `32931218515`.
Artifact: `scene-221508-unobscured-view` / `9593376251`.

The workflow removed all four circle annotations while asserting the saved camera and pose were unchanged, freshly regenerated `Assets/Resources/VoxelShowcase/ShowcaseWorld.bytes`, built and ran the standalone VoxelShowcase, and verified `Replaying issue with 1 screenshot(s). Verified standalone frozen pose.` in `player-run.log`.

## Result

**Visual failure.** The fresh replay still shows the light-blue strip through all three lower marked regions. The market-stall marked region also still contains visible blue exposure around the support contact area. The same long strip is present in the previous attempt-2 replay and the new attempt-3 replay at the exact saved view.

The workflow itself completed successfully, so this is not a stale-bake, pose, player-launch, or capture failure.

See `verification-attempt3-fresh-replay.txt` for the run/artifact identifiers and visual verdict.

## What was learned

The attempt-3 zero-thickness authored-interface hypothesis is **disproven as the complete cause**. Its regression contract is structurally valid, but changing the piazza border overlays to `PaintSolid` and sinking market stalls by one authored decimetre does not remove the assigned visual defects.

This exhausts the allowed full-scene production-fix attempts: **3 / 3 failed visual acceptance**.

## Next

Do not make a fourth production change. Build a bare-bones reproduction that isolates the renderer/authored-boundary behavior behind the surviving blue exposure, then use that reproduction to identify the real owner before any additional production fix.
