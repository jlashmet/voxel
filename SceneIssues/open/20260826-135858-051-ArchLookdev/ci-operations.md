# CI operations

- `7b54d166b75a1b426da285477af30031db2dfeee`: initial exact request; first attempt cancelled during runner/editor contention, one in-place infrastructure retry used.
- `4655349e97d904ded223cf562b011503d446db17`: product compile failure; `ArchitectureVoxelPatterns.cs` could not resolve `SurfaceStyles`; fixed by production import commit `cdfbce730207ee478d22e7750dfba96569afa212`.
- `371a6b0f40198babd9f96ce55b854e94831c0d1b`: product compile failure in the new regression; `KentridgeInteriorScaleTests.cs` lacked the same storage API import; fixed by `b925d43c62aba29855c3d32033bcf401a6ef8264`.

No failed request satisfies the gate. Next request must be built directly on the corrected feature head and left untouched while queued/running.
