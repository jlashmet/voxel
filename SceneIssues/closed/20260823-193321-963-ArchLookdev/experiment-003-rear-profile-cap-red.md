# Experiment 003 — rear profile cap red regression

**Hypothesis** — The retained profile emitter does not close its back-facing annular surface, so
the binary rear opening remains exposed as the marked staircase.

**What was performed** — Added the focused EditMode regression
`ArchProfileStitchTests.RetainedProfileEmitterClosesRearAnnularFace`, which scopes the production
profile emitter and requires both a positive-axis rear normal and the inner/outer back-corner quad.
Ran it through `tools/unity-run.sh` on source `7e5b34d95` before changing production code.

**Result** — The hypothesis was confirmed. One test executed and failed with zero passes because
`EmitProfileBlock` contains neither the rear normal nor the rear annular quad. Unity exited 2 after
11 seconds.

**What was learned** — Full-depth `BackQ4` metadata alone cannot cover the rear face. The emitter
must explicitly publish the back cap, just as it already publishes the front cap and four side
families.

**Next** — Emit one back-facing quad per angular segment from the existing `innerBack*` and
`outerBack*` corners, then rerun the regression and exact visual replay.
