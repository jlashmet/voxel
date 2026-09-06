# GPU resumption — launch regression and retained evidence

## Scope

G01–G04 / G19 continuation from `a551c225d4797abd8b74aaf3889d5736d99e7fed`. This records restoration, not completion of GPU correctness, CPU deletion or 1,000 FPS.

## Proven launch divergence

The only `CpuTransvoxelChunkCache.cs` difference from fetched master `ef475182...` was an unconditional `GpuCutoverDisabled = true` in place of the environment read. Restoring master blob `cf2abdb4624cf49c86fdf47de908cd79a19c1d83` removes that diagnostic gate while preserving current master behavior.

The scenario runner blob `188c7b02fd42d7add4077deee3f22736a2a70b5c` silently ignored `gpuCutover: required`. The module launcher supplies `VOXEL_DISABLE_GPU_CUTOVER=1`, so simply restoring GPU-required scenes would still run CPU. Restore the previously implemented declarative policy in blob `aff1b26f98663edc681a435fca89e2e78c93afc8`; required scenarios clear the child override without modifying the parent environment. Ordinary inherit-mode diagnostics remain temporary migration consumers, not GPU proof.

## Local launch regression

`python3 -m unittest discover -s tools/tests -p test_gpu_player_validation_policy.py -v`

Local copies were verified byte-for-byte by Git blob SHA. Against the old runner: five tests executed, four assertion failures, one pass, zero errors. Against restored runner: five pass, no failures/skips. Tests invoke the real loader/main and inspect the subprocess's effective environment; they also retain capture assertions and reject malformed policies. Subprocess creation is mocked; no Unity execution is claimed.

`GpuCutoverRuntimePolicyTests.CacheUsesConfiguredBackendInsteadOfHardCodedCpuGate` observes the actual cache policy, not source text. It awaits exact-SHA Unity CI. Restored minimal and multi-chunk players exercise production storage, semantic authoring, rendering, traversal/edit/restart and reject fallback. Their historical successes do not certify this restored revision. Candidate-publication metrics require independent outcome and visible-image scrutiny.

## Previous request, now terminal

Request `560b0c08f022c42faa9c6877e63d109083eb2dc9`; source `95d4d30467463b47beb57a731b137da01c56d7d4`; run `34005604349`; job `101412081392`: completed success. Artifact `9981080134`, SHA-256 `082cb6cf6fb47f706103b9fccb0d127f51d1366bc6dfb86c997c8d8917c08405`.

Retained separate player directories:
- `ModuleValidation/Results/Players/Assets_VoxelEngine_Rendering/FarWorldVisibilityDemo-b6d5cf9b30a2b12f/`
- `ModuleValidation/Results/Players/Assets_VoxelEngine_Rendering/WaterDemo-ea6924c728356059/`

Both contain their own logs and screenshots; the prior output collision is no longer present in this artifact. SceneIssue GPU request/publication counts remain zero. The 35.2-second far-feature-disabled screenshot is diagnostic only; final restored images and subsequent GPU views still require inspection. FRAMEPIPE has zero samples and is not performance evidence.

## Active GPU-enabled request — not a runtime pass yet

Feature source `9684ff509d65ab7a1caca6245d0f0093f28e249d`; direct-child request `fb4a7a92de3420c0affa2a5463287d0252f67797`; run `34007154618`; job `101416373122`; created `2026-09-06T02:42:08Z`. Latest observation: queued, no self-hosted macOS runner assigned. Request kept intact. It requests the cache-policy regression, derives module tests/players by repository convention, and includes a 65-second full-scene VoxelShowcase replay. The current default 1600x900 capture is initial diagnosis, not the locked primary 1920x1080 repeated benchmark. Near steps 1/2 may use GPU; coarse/water CPU dependencies still require migration and deletion.

## G19 frame-timing build discriminator

`tools/showcase-player-capture.sh` at exact source above, blob `d15a964c88f17cd103171076c74e4d46ef852d3c`, only requested `-voxelFrameTimingStats` for stationary sampling. Ordinary FPSLOG runs invoke `CaptureUnityFrameTiming()` but lacked the player's build setting. `ShowcasePlayerBuild.cs` already supports this flag and restores `PlayerSettings.enableFrameTimingStats` in finally; reuse it instead of changing global project settings or adding another build path.

`python3 -m unittest discover -s tools/tests -p test_player_capture_frame_timing.py -v`

The regression executes the actual shell script against temporary scene/issue inputs and a recording substitute for the Unity wrapper, which immediately exits with a sentinel. A local pgrep substitute avoids inspecting/waiting for the developer's editor. No Unity, player or fake visual proof is produced. Tests preserve the actual builder/scene arguments, non-development build, failure propagation, cleanup and rejection of incompatible movement.

Verified original blob byte-for-byte. Before: five cases, three intended missing-flag failures (ordinary, traversal, SceneIssue), two passes, zero errors. After requesting the flag unconditionally for diagnostic captures: all five pass. `bash -n` passes. Combined new launch-policy and timing suites: ten tests pass. These local results do not prove Metal timing availability or overhead. The timing patch is a descendant of the queued source and must receive its own exact-source validation after that request terminates. Never replace a queued/running request to include it.
