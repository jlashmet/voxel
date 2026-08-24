# Experiment 004 — rear profile cap green regression

**Hypothesis** — Emitting the missing back-facing annular quad closes the retained cut-stone
profile at `BackQ4`.

**What was performed** — Added one rear quad per existing angular segment using
`innerBack0`, `outerBack0`, `outerBack1`, and `innerBack1`, with its normal facing along the
positive extrusion axis. Re-ran
`ArchProfileStitchTests.RetainedProfileEmitterClosesRearAnnularFace` through
`tools/unity-run.sh` on the working tree based at `7e5b34d95`.

**Result** — The hypothesis was confirmed. One test executed and passed with zero failures in
0.028 seconds; the guarded wrapper exited 0 after 11 seconds.

**What was learned** — Retained profiles now publish all six relevant surface families: projected
front, rear cap, intrados, extrados, and both radial beds. Intentional joint gaps remain because the
rear quad uses the same joint-trimmed angular segment as the other profile faces.

**Next** — Rebuild and replay the exact saved camera to verify the marked rear silhouette visually.
