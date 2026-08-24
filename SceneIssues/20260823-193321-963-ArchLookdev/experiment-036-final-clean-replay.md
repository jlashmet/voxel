# Experiment 036 — final clean exact replay

**Hypothesis** — The retained-profile ownership fix remains correct after removing all diagnostic
logging and bypasses ownership work for ordinary chunks without profiles.

**What was performed** — Removed live counters/logging, gated the filtered append path on the
presence of profile blocks, ran the affected EditMode suite, rebuilt the production `ArchLookdev`
player through `tools/unity-run.sh`, and ran the exact 1637x1140 saved camera for 25 seconds on the
working tree based at `7e5b34d95`. The temporary camera resource was removed after capture.

**Result** — The affected suite passed 91/91. The final exact replay has a continuous smooth
upper-left/crown intrados across every marked region; intentional radial joints remain and no
unrelated scene geometry is missing. The player exited normally after the harness captured its
settled frame. Evidence is `verification-affected-green.txt`, `verification-affected-green.xml`,
`verification-final-build.txt`, `verification-final-player-log.txt`,
`verification-final-pose.png`, and `verification-final-marked-region.png`.

**What was learned** — The fix is stable without diagnostics and scoped away from normal
non-profile topology. The issue is ready for final diff review, production/test/evidence commit,
and separate manifest resolution.

**Next** — Review `git diff`, commit and push the fix/evidence, then update `issue.json` with the
real fix SHA in a separate resolution commit.
