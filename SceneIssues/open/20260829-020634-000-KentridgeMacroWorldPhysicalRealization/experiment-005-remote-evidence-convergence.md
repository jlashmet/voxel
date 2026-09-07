# Experiment 005 — remote evidence convergence

**Hypothesis:** Run `33260139560` contains all named files but captures too soon after remote camera moves; the physical macro content is present, while near-surface publication has not converged and far terrain masks the settlement/lake views.

**Action / source:** Compare `macro-moordell.png` from green run `33259572439` (2.4 s capture dwell) with `33260139560` (0.95 s dwell), and correlate `MACROEVIDENCE screenshot=` with renderer `FAR ... coverage=` / `missingVisible` telemetry.

**Result:** The earlier Moordell frame visibly shows a large grounded stone/roof blockout beside the road. The latest Moordell frame shows only road/terrain. In `33260139560`, remote named screenshots are emitted while renderer telemetry is still `coverage=False` with transient missing-visible counts (Moordell 103, Rossdam 113, Fairy Village 158, Orc Village 104, lake 112, ridge 165). The older Moordell view converges to `coverage=True` immediately after its longer dwell and visibly contains the building.

**Verdict:** Confirmed evidence-timing defect, not missing world content. Pre-generating voxel regions is insufficient because derived near-surface rendering still needs real frames after a teleport.

**Next:** Keep the validation-only prewarm, accelerate only the validation-profile opening timeline so more real seconds remain after gameplay release, restore multi-second target dwell, and capture each target only after near-surface coverage reports complete. Restore `Time.timeScale` before CharacterMotor evidence and on teardown. Do not alter opening content, worldgen, route planning, or ordinary gameplay.
