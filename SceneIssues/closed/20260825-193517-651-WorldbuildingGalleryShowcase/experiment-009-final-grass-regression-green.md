# Experiment 009 — final grass regression green

**Hypothesis**

The complete durable grass implementation, including the live standard-character publisher, satisfies the authored foliage contract and still matches a non-zero focused EditMode test.

**What was performed**

- Production/test source commit: `92515619e714ce3aa899f8eca884d342fadb890b`.
- CI request commit: `949363fe0c62e39abc5c53f55e89c3edf2c28cfe` on `ci-test/fixes/agent-8`.
- GitHub Actions run `32933720105`, job `98070821947`.
- Requested `VoxelEngine.Tests.EditMode.ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract` on EditMode.

**Result**

The job completed successfully. Unity returned status 0 and the workflow explicitly reported `Executed 1 test case(s).`; the final `ci/single-test` status was published as success. The result artifact was `single-test-32933720105` (artifact id `9594408691`).

**What was learned**

The focused regression now proves the complete shader/material/registry/game-publisher contract rather than only the earlier shader-only state. The prior queue condition recorded in experiment 008 was infrastructure delay, not a test failure.

**Next**

Integrate current master while preserving both histories, run the smallest existing character-equipment regression affected by the new lifecycle hook, and replay the original saved gallery fixture again from the complete integrated fix before terminal bookkeeping.
