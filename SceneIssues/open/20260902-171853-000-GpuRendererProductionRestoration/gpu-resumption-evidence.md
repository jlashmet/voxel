# GPU resumption — launch regression and retained evidence

## Scope

G01–G04 / G19 continuation from `a551c225d4797abd8b74aaf3889d5736d99e7fed`. This records restoration, not completion of GPU correctness, CPU deletion or 1,000 FPS.

## Proven launch divergence

The only `CpuTransvoxelChunkCache.cs` difference from fetched master `ef475182...` was an unconditional `GpuCutoverDisabled = true` in place of the environment read. Restoring master blob `cf2abdb4624cf49c86fdf47de908cd79a19c1d83` removes that diagnostic gate while preserving current master behavior.

The current scenario runner blob `188c7b02fd42d7add4077deee3f22736a2a70b5c` silently ignored `gpuCutover: required`. The module launcher supplies `VOXEL_DISABLE_GPU_CUTOVER=1`, so simply restoring GPU-required scenes would still run CPU. Restore the previously implemented declarative policy in blob `aff1b26f98663edc681a435fca89e2e78c93afc8`; required scenarios clear the child override without modifying the parent environment. Ordinary inherit-mode diagnostics remain temporary migration consumers, not GPU proof.

## Local behavioral regression

`python3 -m unittest discover -s tools/tests -p test_gpu_player_validation_policy.py -v`

A local copy was verified byte-for-byte by Git blob SHA against each source above. Against the old runner: five tests executed, four assertion failures, one pass, zero errors. Against restored runner: five pass, no failures/skips. Tests invoke the real loader/main and inspect the subprocess's effective environment; they also retain capture assertions and reject malformed policies. Subprocess creation is mocked; no Unity execution is claimed.

The added `GpuCutoverRuntimePolicyTests.CacheUsesConfiguredBackendInsteadOfHardCodedCpuGate` observes the actual cache policy rather than reading source text. It awaits exact-SHA Unity CI. Restored minimal and multi-chunk players exercise production storage, semantic authoring, rendering, traversal/edit/restart and fail on fallback; their historical successes do not certify this restored revision. Existing candidate-publication metrics require independent outcome and visible-image scrutiny.

## Previous request, now terminal

Request `560b0c08f022c42faa9c6877e63d109083eb2dc9`; source `95d4d30467463b47beb57a731b137da01c56d7d4`; run `34005604349`; job `101412081392`: completed success. Artifact `9981080134`, SHA-256 `082cb6cf6fb47f706103b9fccb0d127f51d1366bc6dfb86c997c8d8917c08405`.

Retained separate player directories:
- `ModuleValidation/Results/Players/Assets_VoxelEngine_Rendering/FarWorldVisibilityDemo-b6d5cf9b30a2b12f/`
- `ModuleValidation/Results/Players/Assets_VoxelEngine_Rendering/WaterDemo-ea6924c728356059/`

Both contain their own logs and screenshots; the earlier output collision is no longer present in this artifact. SceneIssue GPU request/publication counts remain zero. The 35.2-second far-feature-disabled screenshot is diagnostic only; final restored images and subsequent GPU views must still be inspected. FRAMEPIPE has zero samples and is not performance evidence. No CPU-only diagnostic replay is scheduled ahead of GPU resumption.
