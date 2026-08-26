# Experiment 003 — Corrected targeted glazing regression

**Hypothesis**

After correcting the bytecode operand offsets in the regression, the thin-centered-pane implementation should satisfy the focused geometry invariant in both facade orientations.

**What was performed**

Ran targeted EditMode CI for `VoxelEngine.Tests.EditMode.ArchitectureVoxelPatternTests.GlazedOpeningUsesThinCenteredPaneAcrossFacadeOrientations` from CI request commit `6ca949773640234d5d74e8ed15c9bc3b90c571f6`, whose feature source is `cf3abdcfe5c662f5cb744f6c64bc29c14cf7934d` and which contains production fix `86b6b547aaa5dddf3cca05d4e0d44353613e35ce` plus corrected regression `7ca3611c1be2daa1cea730d5ce1f1e08ae0bf8fa`.

**Result**

GitHub Actions run `32993908437` completed successfully. The requested Unity test executed and passed; the workflow also completed its result upload and final `ci/single-test` status publication successfully.

**What was learned**

Hypothesis confirmed. The production implementation preserves the full carved reveal while restoring only a thin centered glazing pane, and the corrected focused regression now verifies that invariant without reading reserved bytecode words as operands.

**Next**

Fresh-bake `VoxelShowcase` and replay the original saved camera in the standalone player. Inspect the circled window region before closing the capture.
