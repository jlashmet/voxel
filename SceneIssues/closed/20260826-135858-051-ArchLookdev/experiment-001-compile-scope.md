# Experiment 001 — compile scope

**Hypothesis:** the final CI failure after the production import fix is still a geometry/runtime defect.

**Action / source:** inspected exact request `371a6b0f40198babd9f96ce55b854e94831c0d1b` from source `6efb883d521f7c1c6c58104e185ae9203da02891` and compared the compiler diagnostic with the production/test imports.

**Result:** Unity stops before test execution at `KentridgeInteriorScaleTests.cs:144`: `SurfaceStyles` is unresolved. The regression file lacked `VoxelEngine.Storage.Api`; the production file had already been corrected. No replay frames were produced because compilation aborted.

**Verdict:** falsifies a runtime/geometry failure for this request. This is a test compile-scope defect. Commit `b925d43c62aba29855c3d32033bcf401a6ef8264` adds only the missing test import.

**Next:** final exact-SHA PlayMode regression plus saved-pose replay.
