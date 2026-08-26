# Experiment 010 — isolate the first moving zero-draw frame

**Hypothesis** — The frame-5 traversal failure is not a generic clipmap discovery failure. The first bad frame should be classified by the known→in-band→frustum funnel and step-4 readiness before production changes.

**Action / source** — On `35a79439afaeb7c4170dc355978c9e10541d9859`, added `ShowcaseTraversalCoverageDiagnosticsTests.ShortFlyTraversalKeepsAtLeastOneDrawableSurface`, preserving the first 20 frames of the production fly traversal and reporting aggregate visibility plus step-4 state. Targeted request `02bf75c3dc7a48cc331196ed91d4af3aecdf4c82`, run `33016648361`.

**Result** — Failed before traversal. After 1,200 setup frames the test never reached four visible frames: `known=123 resident=13 dirty=18 jobs=1 candidates=123/31/0`; step 4 was `known=92 resident=0`, with `visibility=92/0/0/0/0`. The visibility funnel therefore collapsed at frustum culling during setup, unlike the earlier production traversal that reached a valid visible gate and then failed on movement frame 5.

**Verdict** — Inconclusive for the product hypothesis. This run cannot distinguish LOD publication from clipmap routing because it never reproduced the prerequisite visible view. It does prove the diagnostic must remove initial-view variability before any scheduler change is justified.

**Next** — Pin the exact saved SceneIssue camera pose, already verified by replay as visible, before warmup and repeat the short traversal. Production remains unchanged.