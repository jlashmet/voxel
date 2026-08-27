# Experiment 002 — regression scope

**Hypothesis:** the failed targeted run means the 160-dm production pitch still leaves same-frontage houses cramped.

**Action / source:** inspected feature head `b19345f1bfd8b7d8917590332746d96be97be45c`, `KentridgeUrbanFabricCatalogue`, `KentridgeFrontageAlignedUrbanFabricCatalogue`, and the failing PlayMode regression. The base catalogue stores each run's anonymous 72-dm envelopes on a constant cross-axis coordinate. The frontage-alignment adapter later shifts each envelope only along its facade-normal axis according to the generated house depth; it does not change lateral frontage spacing.

**Result:** the regression grouped any same-orientation envelopes whose cross-axis bounds overlapped after that depth-dependent normal shift. That admits houses from separate parallel frontage runs and tests a relationship the accepted defect does not require. The production packing experiment still yields 25 dm minimum same-frontage envelope clearance at 160-dm pitch.

**Verdict:** the CI failure is a regression-scope false positive, not evidence for a larger production pitch. Keep the localized production fix. Correct the behavioral regression to build the production anonymous catalogue before frontage alignment, group by orientation plus exact authored cross-axis frontage coordinate, sort adjacent envelopes along that line, and require >=20 dm.

**Next:** commit the regression correction and issue one final exact-SHA targeted PlayMode + saved-pose replay request.
