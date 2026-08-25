# Experiment 005 — final exact-pose replay

**Hypothesis** — The regression-only production accessor and test assembly wiring leave the
already-correct reference foliage unchanged in the final ordinary ArchLookdev player.

**What was performed** — After the affected suites passed, rebuilt `ArchLookdev.unity` through
`tools/unity-run.sh` from the final working tree based at
`847bac34f4c34b8cb6ca1130bb968efcf6f3598d`. Ran the production macOS player at the saved
1637×1140 `Hero Arch Camera` pose and 34-degree FOV for 25 seconds. Inspected the settled
24-second frame against all five circles and `References/arch_reference.png`; then removed the
temporary camera resource. Evidence is `verification-final-build.txt`,
`verification-final-player-log.txt`, and `verification-final-pose.png`.

**Result** — Confirmed. The player build succeeded and exited normally. The final frame preserves
dense ivy and visible flower heads along the left pier, haunch, and crown, plus discrete growth
islands at the marked right shoulder and pier. The right side retains the reference's intentional
bare-masonry separation. No marked region is missing its intended growth.

**What was learned** — The post-capture foliage fix remains stable in the final production player,
and the new semantic regression does not perturb presentation. This SceneIssue is ready for diff
review, a fix/regression/evidence commit, and separate resolution bookkeeping.

**Next** — Review the final diff and repository status, commit and push the issue fix evidence,
then resolve `issue.json` with that real commit SHA in a second commit.
