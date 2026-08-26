# Experiment 012 — final clean grass regression

## Purpose
Validate the complete durable agent-8 source after removing the temporary replay router and after the final saved-fixture replay evidence was committed.

## Request
- Durable feature head under test: `9fc2aa3a8721e6ceb62bf4afeece75467a2abcd7`.
- CI request commit: `301616e88b5f147df8f3e32eb981dd86a44c6e11` on `ci-test/fixes/agent-8`.
- Request id: `agent-8-20260826-grass-final-clean-012`.
- Test: `VoxelEngine.Tests.EditMode.ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract`.
- GitHub Actions run: `32942538397`.
- Result artifact: `single-test-32942538397`, artifact id `9597057795`.

## Evidence
Unity finished with status 0. The workflow's zero-match guard reported `Executed 1 test case(s).` The job completed successfully and published `ci/single-test` success.

## Result
PASS. This is the final clean targeted regression for promotion; the tested source contains no replay-only routing and no feature-branch `.github/test-request.json` change.
