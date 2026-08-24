# Experiment 006 — fixed streaming replay

**Hypothesis** — With near/far low-surface parity restored, exact-pose frames remain grass-colored
through partial residency and converge without the reported broad brown patches.

**What was performed** — Rebuilt the production `VoxelShowcase` macOS player through
`tools/unity-run.sh` from the fixed working tree based at `87bfc27d7`, then repeated the exact saved
1293×718 camera replay for 35 seconds with screenshots every second. Evidence is in
`verification-fixed-transient-window.png` and `verification-fixed-settled.png`.

**Result** — The hypothesis was confirmed. At 19.3 seconds, while only 381 surfaces are drawn and
77 remain missing, the formerly brown fallback regions are grass green. They remain green at 20.3
seconds with 436 drawn and 22 missing. The view reaches 458 drawn and zero missing at 25.8 seconds;
the 26.8-second settled frame retains the same ground-cover identity and only the narrow authored
dirt features remain.

**What was learned** — The fix removes the reported material pop throughout the same convergence
window that reproduced it before. Geometry detail still streams normally, but absent near coverage
no longer changes the semantic ground material from dirt to grass when it publishes.

**Next** — Run the broader affected far-fallback contract tests, review the clean diff and temporary
wiring, then commit and push the fix/evidence before resolving `issue.json` separately.

## Metrics

- `t=18.3`: drawn 165, missing 345, fallback grass
- `t=19.3`: drawn 381, missing 77, fallback grass
- `t=20.3`: drawn 436, missing 22, fallback grass
- `t=25.8`: drawn 458, missing 0
- fixed transient-window SHA-256: `b00804387330213b538718543e8941cb77edb36c46cdfbdafb7327a83248c2a8`
- fixed settled SHA-256: `67447102e9616e0ba9568a2a8b7604da69b024c710a89e33e176096e09d698ef`
