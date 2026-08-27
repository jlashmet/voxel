# Experiment 001 — circulation owner

**Hypothesis.** The captured stair defect is caused by missing vertical-circulation composition, not camera/cardinal placement.

**Action / source.** Inspected the captured issue (no circles; saved pose at `-414.095,9.142,-321.975`) and production source at base `94d390cac3fda5199a87033e2cae5bbd5f65287f`: `HouseProgramCompiler` fills intermediate slabs; Kentridge generated-house decoration adds furniture; existing shared APIs already define `StairConfig`, `LandingConfig`, and `InteriorConnectionKind.Stairwell`.

**Result.** No stair-aligned slab carve or upper guard exists in the local generated-house program before world placement/orientation. Therefore a transform-only fix cannot produce the requested structured opening.

**Verdict.** Supported. Own the fix at generated-house circulation composition, using shared stair constraints; final saved-pose replay will verify the capture view.
