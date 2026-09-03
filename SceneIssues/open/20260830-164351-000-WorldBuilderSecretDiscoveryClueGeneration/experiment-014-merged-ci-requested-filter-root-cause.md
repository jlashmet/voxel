# Experiment 014 — merged-CI requested-filter root cause

## Hypotheses

1. **The regression name became stale during the module-local test move.** If true, the merged source would no longer compile/discover `CaveSecretPocketCluePresentationTests` under its prior fully qualified name.
2. **The master merge changed the CI execution path, and the new persistent requested-test phase fails to select an otherwise valid test.** If true, the same test remains compiled/discoverable, while only the new requested phase reports zero executed cases.

## Action / evidence

Source under test was `ad88baa75dd3926d80e172d10fe12cfccfd7f028`.

- Run `33654878544` used the class filter `VoxelEngine.Tests.EditMode.CaveSecretPocketCluePresentationTests` and failed with `requested filter matched zero tests` after Unity exited 0.
- Run `33714475042` used the exact previously-green method filter `VoxelEngine.Tests.EditMode.CaveSecretPocketCluePresentationTests.BoundaryEvidenceIsDeterministicFractureOnCaveFaceAndPreservesVerifiedSeal` and failed identically after Unity exited 0.
- The second run's Unity log compiles `CaveSecretPocketCluePresentationTests.cs`, then reports `editmode-0 discovered 918 test cases` and `requested discovered 918 test cases`.
- Its `persistent-requested.txt` reports `result_state=Passed` but `passed=0`, `failed=0`, `skipped=0`, `inconclusive=0` for the exact method filter.
- The last known-green exact run `33537413920` used the same method name, but the then-current workflow executed the focused regression through direct Unity CLI `-runTests -testFilter` before automatic module validation.
- Between that green SHA and the merged SHA, `.github/workflows/tests-single.yml` and `Assets/Editor/CI/VoxelCiPersistentTestRunner.cs` changed. Push requests now inject compatible focused tests into the persistent editor via `Filter.testNames`; the focused gate is explicitly documented by the workflow as an optional extra gate.

## Verdict

Hypothesis 1 is falsified. The test still compiles and the exact method identifier is unchanged from the previously green request.

Hypothesis 2 is supported: the repeated zero-match symptom is an infrastructure regression in the post-merge **optional persistent requested-test path**, not evidence that the WorldBuilder regression failed. The persistent requested phase loads the full EditMode test tree but returns a zero-case successful result when `Filter.testNames` is supplied.

## Next step

Do not attempt a third filter spelling. Retry exact-SHA validation on the same `ci-test/fixes/agent-5` transport with no optional requested test. This uses the repository-defined required automatic WorldBuilder assembly validation plus convention-discovered SecretDiscovery player validation and Kentridge integration, while avoiding the isolated optional-filter infrastructure defect. The behavioral regression remains owned by the automatically required WorldBuilder test assembly and already has direct focused proof from run `33537413920`.
