# CI operations

- `7b54d166b75a1b426da285477af30031db2dfeee`: initial exact request; first attempt cancelled during runner/editor contention, one in-place infrastructure retry used.
- `4655349e97d904ded223cf562b011503d446db17`: product compile failure; `ArchitectureVoxelPatterns.cs` could not resolve `SurfaceStyles`; fixed by production import commit `cdfbce730207ee478d22e7750dfba96569afa212`.
- `371a6b0f40198babd9f96ce55b854e94831c0d1b`: product compile failure in the new regression; `KentridgeInteriorScaleTests.cs` lacked the same storage API import; fixed by `b925d43c62aba29855c3d32033bcf401a6ef8264`.
- `f0657d7a5f4ba28d26296aee89b85a0647a66330` from source `37987ed3b649a68f5b5d28509e8162bcf590fc0c`: focused test and real-player workflow steps were green, but direct artifact inspection failed the SceneIssue visual gate. No frame showed the hero arch and `player-run.log` had no `ARCH_EVIDENCE` activation. The built-player launcher passes `-voxel-scene-issue`; the landmark harness parsed only `-voxelIssue`. Treat as a product evidence failure, not a successful gate.
- `1782404ce6aeb3e5a5782b04c4f7854acd6a8279` from source `1de828cf0c719dac85e916576f728b93a671e239`: request admission failed before Unity because `replay_seconds` was `80`, outside the workflow contract of 20–60 seconds. No tests/player ran and no artifact was produced. This is a request-configuration product failure; correct the assigned CI request to a valid replay window before rerunning.

No failed or visually invalid request satisfies the gate. The next request must be built directly on the corrected feature head and left untouched while queued/running.
