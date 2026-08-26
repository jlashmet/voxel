# Experiment 004 — Exact replay still reads as a flat amber pane

**Hypothesis**

After making rectangular glazing thin and centered within the wall reveal, the captured Kentridge facade should read as an architectural window rather than a full-depth glass block.

**What was performed**

Ran the fresh-baked exact saved-camera replay from CI commit `beea1c5c5197ad479a18be56d6f978f06de5d27b` in GitHub Actions run `32997204734`. The workflow baked a new `ShowcaseWorld.bytes`, built the standalone `VoxelShowcase` player, applied `SceneIssues/open/20260826-132429-861-VoxelShowcase/issue.json` through `--scene-issue`, used the captured `1928x836` resolution and camera pose, ran for 50 seconds, and captured four screenshots. The selected final frame was `showcase-003-t044.6s-stationary.png`.

The replay operation itself succeeded: the world bake exited 0, the player build exited 0, the player exited 0, and the late replay reached `missingVisible=0`. The workflow's overall failure happened afterward while trying to persist evidence because generated bake/build files were still unstaged before `git pull --rebase`. The replay artifact `sceneissue-132429-replay-32997204734` was uploaded successfully and was inspected separately.

**Result**

Failed visual acceptance. The saved camera still shows large uninterrupted amber rectangles surrounded by an awkward dark/blue rounded recess. Reducing hidden pane depth fixed a geometry invariant but did not materially fix the user-visible complaint: the openings still read as glowing slabs rather than constructed windows.

**What was learned**

The dominant defect is facade composition, not only wall-depth ownership. A rectangular window needs visible architectural framing/subdivision in the facade plane. The existing warm glazing material exaggerates the problem when one pane spans the whole opening.

**Next**

Add a focused regression for inset/framed rectangular glazing that preserves facade masonry as an outer border and center mullion, then make the smallest Kentridge-facing production change so large rectangular warm panes are subdivided instead of emitted as one uninterrupted slab. Re-run targeted CI and the exact saved-camera replay before considering closure.
