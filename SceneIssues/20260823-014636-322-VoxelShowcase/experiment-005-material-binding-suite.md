# Experiment 005 — material-binding suite

**Hypothesis** — Aligning low-surface ground cover preserves every other game-owned showcase role
and structural classification.

**What was performed** — Ran the complete EditMode fixture
`Game.Materials.Tests.GameShowcaseMaterialTests` through `tools/unity-run.sh` on the fixed working
tree based at `87bfc27d7`.

**Result** — The hypothesis was confirmed. Four tests executed and all four passed with zero
failures in 0.023 seconds; the guarded wrapper exited 0 after 9 seconds.

**What was learned** — The fix changes only the intended low terrain surface. Deep/subsurface,
authored dirt, structure roles, near/far parity, and structural classification remain covered.

**Next** — Rebuild the production player and replay the exact capture through the same convergence
window that previously exposed the brown fallback.
