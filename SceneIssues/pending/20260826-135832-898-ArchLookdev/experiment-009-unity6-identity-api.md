# Experiment 009 — Unity 6 mesh identity API

## Hypothesis
Experiment 008 is runtime-viable; the red targeted run is compile-scoped because the new detail pass used an API this Unity version has promoted from deprecated to an error.

## Action / source
Exact-source request for feature SHA `917ca66b35a6ce8dd669372fe32ecbb6f61e4345`: run `33130080549`, test `ArchReferenceGrowthDetailPassTests.CloseUpRefinementAddsLeafDepthAndIrregularBlossomsAcrossRebuild` plus the original 45-second scene replay.

## Result
Unity aborted before tests or player build. The only new compile errors are `CS0619` at `ArchReferenceGrowthDetailPass.cs:108-109`: `Object.GetInstanceID()` is obsolete in Unity 6000.5.6f1 and rejected. No runtime or pixel evidence was produced, so this run says nothing about the depth/scale art hypothesis.

## Verdict / next step
Confirmed compile-only blocker. Replace integer instance IDs with direct `Mesh` reference identity; that changes only rebuild detection and leaves mesh mutation, placement, lifecycle, draw count, and art parameters untouched. Re-run the same focused regression and saved-pose replay on a fresh exact source SHA.
