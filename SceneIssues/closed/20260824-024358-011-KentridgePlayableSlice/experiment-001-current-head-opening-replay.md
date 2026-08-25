# Experiment 001 — current-head opening replay

**Hypothesis** — Current `fixes` still presents the opening cast beneath a grass/terrain surface at
the saved view, despite the bounded pub roof cutaway.

**What was performed** — Built the ordinary production `KentridgePlayableSlice` macOS player
through `tools/unity-run.sh` from source `3d0923b829b41d337cdfe40af9677176865a2a1a`, pinned the
saved 1637×1140 `Kentridge Player Camera` pose at 58-degree FOV, and ran for 50 seconds. Inspected
the establishing and first-dialogue frames at 31.9 and 46.9 seconds. Evidence is
`verification-current-build.txt`, `verification-current-player-log.txt`, and
`verification-current-pose.png`.

**Result** — Confirmed. The opening starts and actor heads/upper bodies move through the frame, but
a stepped grass surface covers nearly the entire marked conversation area. The gray authored pub
surface remains visible beside it. The defect persists after convergence and during line 01; the
player harness reports no assertion failures and exits normally.

**What was learned** — The opening camera/cutscene lifecycle and actor realization work. The
bounded roof cutaway is insufficient because the obscuring surface sits at ground-floor/actor-body
height rather than in the upper pub volume that presentation intentionally hides. This points to
terrain/site preparation or interior clearing in authoritative world realization, not a camera
near-plane or roof-presentation failure.

**Next** — Measure the generated pub plot base, local terrain heights, floor/interior carve, and
stage points. Identify where terrain occupancy is supposed to yield to the building footprint
before changing either authoring or presentation.
