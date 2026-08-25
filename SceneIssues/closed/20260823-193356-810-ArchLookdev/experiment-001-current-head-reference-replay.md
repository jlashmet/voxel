# Experiment 001 — current-head reference replay

**Hypothesis** — The saved camera still lacks the reference-inspired ivy and flowers on current
`fixes`, indicating a live authoring, lifecycle, or vegetation-rendering defect.

**What was performed** — Built the ordinary production `ArchLookdev` macOS player through
`tools/unity-run.sh` from source `847bac34f4c34b8cb6ca1130bb968efcf6f3598d`, then pinned the
saved 1637×1140 `Hero Arch Camera` pose at 34-degree FOV for 25 seconds. Compared the settled
24-second frame against the focused tracked target `References/arch_reference.png` (derived from
the broader `sunlit-cleric-reference.png`) and inspected every one of the five saved circles.
Evidence is `verification-current-build.txt`,
`verification-current-player-log.txt`, and `verification-current-pose.png`.

**Result** — The hypothesis was disproven on current `fixes`. The settled production frame shows
dense ivy with flower heads from the left pier through the left haunch and crown, plus separated
ivy islands on the right shoulder and pier. All five marked regions now contain the intended
growth, while bare masonry remains visible on the right. The player exited normally. The capture
was recorded at 2026-08-23 12:33:56 PDT; repository history shows the final foliage-presentation
commit `dde64c8fe` landed at 12:37:18 PDT, after the broken capture.

**What was learned** — The issue was already corrected immediately after capture, but its
`issue.json` was never resolved. Current behavior is deterministic authored vegetation submitted
through the production instanced renderer, not voxel topology and not CPU/GPU Transvoxel work.

**Next** — Audit `dde64c8fe` and the existing focused tests for the authored distribution and
renderer lifecycle. If they prove the invariant, run those tests and a clean final replay before
recording the existing production commit as the fix.
