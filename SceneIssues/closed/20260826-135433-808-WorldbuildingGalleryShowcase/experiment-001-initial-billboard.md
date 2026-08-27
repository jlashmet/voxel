# Experiment 001 — initial billboard candidate

**Hypothesis:** The marked defect is caused by radial tuft cards not presenting like the referenced camera-facing pixel grass.

**Action / source:** Specialized the shared grass-like shader path on source `de5ccd3fa8f7f3f9fa8d59b3551dd3dde32eb634` and requested PlayMode framebuffer + saved-pose replay in run `33043258034`.

**Result:** The test harness failed before assertions because `WaitForEndOfFrame` is unsupported in this batchmode path. The saved-pose replay still completed and showed the candidate as several tall dark vertical bars inside the marked region.

**Verdict:** Product candidate rejected; the visual defect was real, and the regression harness also needed explicit `Camera.Render()`.

**Next:** Fix the harness and isolate semantic Grass from other foliage before retesting.
