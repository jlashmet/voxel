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
- [x] Inspect all five direct-parent PNGs: the multidraw survey corruption is absent. Existing startup incompleteness and coarse/far-field holes remain and stay owned by the parent plan's D2/D3 work.
- [x] Pin `fbaf77b8d210cc5e5f98bd99c0bf50e8640a7ac2` as the garbling regression boundary: its direct parent is clean of the cross-world survey corruption and `109dc042…`, which contains it, reproduces that corruption.
- [x] Build controlled source `20b200cad353f5971024729db12b34ab1f21bb73` by retaining the current real-player harness and restoring the 15 files changed by the indexed-multidraw commit to their direct-parent versions.
- [x] Run the same real-player `CastleScreenshotTests` capture against that controlled current-harness/pre-multidraw source in run `32497237439`; PlayMode and standalone-player capture both pass, the survey frames contain no giant slabs/cross-world triangles, and the Metal player log has no `SmoothSurface` shader-support error.
- [x] Restore the Metal-validated bucketed solid submission path from the clean parent on `rendering-garbled` (`075d672337e236642c729025bfa89248ccc18081`), including its architecture regression; do not retain the unvalidated indexed-multidraw path or unsupported `SV_DrawID` experiment.

### D. Validate the repair

- [x] Run the smallest focused EditMode regression for the restored bucketed submission path and require green CI (`GeometryPipelineArchitectureTests.SolidSurfaceDrawsAreBucketedInsteadOfSubmittedPerChunk`, run `32497707003`).
- [x] Run `VoxelEngine.Tests.PlayMode.CastleScreenshotTests` on the fixed `rendering-garbled` branch through the real standalone-player capture path (run `32498578646`, request commit `32f846b32cd0c1d9fdde996fc1f29c11d9dbdead`); PlayMode and real-player capture both pass.
- [x] Inspect all five final captured PNGs. The four survey frames no longer contain the indexed-multidraw giant slabs/cross-world triangles or disconnected cross-world geometry. The 14.5 s stationary frame still contains the pre-existing incomplete startup presentation, but it is byte-identical (`sha256 b0d484b9d7bc383e847a3902d97c05e4e30540e14d7be772087d8f76609ea263`) to the clean direct-parent baseline and is not part of this regression.
- [x] Inspect the final player log: no `SmoothSurface` shader-support error, no `SV_DrawID`/indexed-indirect shader failure, and the harness finishes after 60 s with `assertion failures 0` (run `32498578646`).
- [x] Compare `rendering-garbled` with base `109dc042cf8079b0c582d4b1f5196a09cf367bc6`: the branch contains this focused plan plus the exact 15 renderer/test files reverted from `fbaf77b8…`; there are no unrelated production edits or leftover `SV_DrawID` experiment.
- [x] Mark this focused garbled-rendering repair complete: the real-player indexed-multidraw corruption is removed and validated on Metal. Do not close the parent plan's independent D2/D3 startup/coarse/far-field coverage tasks; those remain separately open.

### E. Reimplement the FPS optimization without reopening the corruption

- [x] Establish the clean bucketed standalone-player performance/correctness baseline from run `32498578646`: over the stable 35-60 s survey window excluding screenshot stalls, mean FPS is 307.16 and mean frame time is 3.28 ms. Preserve its five screenshots as the comparison reference.
- [x] Quantify the intended upside from the original but visually invalid indexed-multidraw run `32494256445`: over the same stable window excluding screenshot stalls it averaged 390.66 FPS / 2.56 ms, so the submission collapse is worth recovering if correctness is retained.
- [ ] Add a focused regression for the actual indexed-draw addressing contract before changing production: every hardware index stored in the shared contiguous surface arena must remain chunk-local, and each indirect command must provide that chunk's `startIndex` plus `baseVertexIndex` exactly once.
- [ ] Reintroduce a Metal-compatible indexed-indirect arena representation without changing geometry ownership, publication, LOD, or frame budgets. Keep the contiguous `SurfaceGeometryArena` index payload chunk-local; do not apply the vertex base both in the stored index and in the indirect command.
- [ ] Reimplement the one-call solid submission using Unity's supported indirect shader contract (`InitIndirectDrawArgs(0)`/`GetIndirectVertexID`) without `SV_DrawID`, while preserving the bucketed path as the non-game/editor fallback until the optimized path is proven across the real player.
- [ ] Validate the smallest focused EditMode architecture/addressing regressions on the exact optimization head.
- [ ] Run `VoxelEngine.Tests.PlayMode.CastleScreenshotTests` through the standalone-player capture path on the optimized head; inspect all five PNGs against run `32498578646`, not just the PlayMode assertion.
- [ ] Inspect the optimized Metal player log for shader support/errors and require `assertion failures 0`; reject the optimization if the renderer becomes unsupported or produces cross-world geometry.
- [ ] Compare the stable 35-60 s FPS/frame-time window against the clean bucketed baseline. Keep the optimization only if it produces a repeatable frame-time improvement without reducing rendered coverage or fidelity; otherwise retain the bucketed renderer and identify the next measured submission hotspot.
- [ ] Review the final diff and mark this optimization follow-up complete only when the visual comparison, focused regressions, Metal log, and measured FPS comparison all pass. Parent D2/D3 remain independent.

## Completion evidence

- Original corrupted real-player run: `32494256445`.
- Rejected `SV_DrawID` real-player run: `32495744003`.
- Clean direct-parent baseline: `32496458837`.
- Clean current-harness/pre-multidraw confirmation: `32497237439`.
- Focused bucketed-submission EditMode guard: `32497707003`.
- Final fixed-branch standalone-player validation / optimization baseline: `32498578646`.
