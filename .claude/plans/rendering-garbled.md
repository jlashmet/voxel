# Rendering Garbled — VoxelShowcase Real-Player Repair

This is a focused continuation of `voxel-showcase-rendering-repair-v2.md` for the player-visible geometry corruption reproduced on `rendering-garbled`. The parent plan's D2 near-field residency/pin-reject work and D3 near/far coverage work remain independent and must not be marked complete by this investigation.

## Acceptance rules

- Use the real `VoxelShowcase` standalone-player capture path (`VoxelEngine.Tests.PlayMode.CastleScreenshotTests`) as the visual acceptance gate.
- A green PlayMode assertion is not sufficient: inspect every captured presented-frame PNG.
- Fix the first proven rendering invariant; do not weaken unrelated rendering, LOD, or performance thresholds.
- Keep Metal/macOS support intact; shader changes that make `Hidden/VoxelEngine/SmoothSurface` unsupported are invalid.
- Preserve the parent rendering-repair plan's asynchronous publication and frame-budget constraints.

## Task list

### A. Reproduce and classify

- [x] Create `rendering-garbled` from the requested master head and run the real-player `CastleScreenshotTests` capture.
- [x] Inspect the original standalone-player screenshots and confirm severe geometry corruption: giant slabs/triangles and disconnected castle fragments rather than a simple missing-terrain hole.
- [x] Trace the regression window to the indexed-indirect solid submission change (`fbaf77b8d210cc5e5f98bd99c0bf50e8640a7ac2`) and its surrounding arena/index-addressing changes.

### B. Reject the first false lead

- [x] Test the hypothesis that multi-draw command zero was incorrectly reused by changing `InitIndirectDrawArgs` to consume `SV_DrawID` and guarding that behavior in EditMode.
- [x] Run the focused architecture regression for that hypothesis successfully.
- [x] Rerun the same real-player capture and inspect the screenshots rather than trusting the green workflow.
- [x] Reject the `SV_DrawID` change as the fix: the capture remains wrong and the Metal player reports `Hidden/VoxelEngine/SmoothSurface` unsupported with that shader signature.
- [x] Remove the unsupported experiment from `rendering-garbled`; restore the branch to the clean original source head before the next controlled test.

### C. Isolate the indexed-multidraw regression

- [x] Run the direct-parent (`a9aa6c5707decc7faa3f718eaa1216aebe2ca6b1`) standalone-player baseline through `CastleScreenshotTests` in run `32496458837`.
- [x] Inspect all five direct-parent PNGs: castle and terrain geometry are coherent and the giant slabs/cross-world triangles are absent. Existing coarse/far-field holes remain and stay owned by the parent plan's D2/D3 work.
- [x] Pin `fbaf77b8d210cc5e5f98bd99c0bf50e8640a7ac2` as the garbling regression boundary: its direct parent is clean and `109dc042…`, which contains it, reproduces the corruption.
- [x] Build controlled source `20b200cad353f5971024729db12b34ab1f21bb73` by retaining the current real-player harness and restoring the 15 files changed by the indexed-multidraw commit to their direct-parent versions.
- [ ] Run the same real-player `CastleScreenshotTests` capture against that controlled current-harness/pre-multidraw source as confirmation.
- [ ] Restore the Metal-validated bucketed solid submission path from the clean parent and keep its architecture regression, rather than retaining the unvalidated indexed-multidraw path or the unsupported `SV_DrawID` experiment.

### D. Validate the repair

- [ ] Run the smallest focused EditMode regression for the restored bucketed submission path and require green CI.
- [ ] Run `VoxelEngine.Tests.PlayMode.CastleScreenshotTests` on the fixed `rendering-garbled` branch through the real standalone-player capture path.
- [ ] Inspect all captured PNGs and verify the giant slabs/triangles and disconnected geometry are gone.
- [ ] Inspect player logs for shader support/errors and require `Hidden/VoxelEngine/SmoothSurface` to remain supported on Metal.
- [ ] Compare `rendering-garbled` with its master base and confirm only the intended renderer/test/plan changes remain.
- [ ] Mark this repair complete only after the real-player images are clean; do not close the parent plan's independent D2/D3 tasks unless separately validated.
