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

- [x] Establish the clean bucketed standalone-player performance/correctness baseline from run `32498578646`: over the stable 35-60 s survey window, mean FPS is 307.16 and mean frame time is 3.28 ms. Preserve its five screenshots as the comparison reference.
- [x] Quantify the intended upside from the original but visually invalid indexed-multidraw run `32494256445`: over the same stable window it averaged 390.66 FPS / 2.56 ms, so the submission collapse is worth recovering only if correctness is retained.
- [x] Add a focused regression for the actual indexed-draw addressing contract before changing production (`IndexedIndirectSubmissionArchitectureTests.IndexedIndirectSubmissionAppliesArenaOffsetsExactlyOnce`). The pre-optimization red gate fails as expected in run `32500993168`, while the existing GPU `WritingIntoAnOffsetRangeMatchesWritingAtZeroAndTouchesNothingElse` regression defines the chunk-local index invariant.
- [x] Prove and repair the index-buffer usage contract before the Metal acceptance run. `IndexedArenaRemainsGpuWritableWhileServingAsIndexBuffer` fails on candidate 1 in run `32502292557` because the compute-written `RWByteAddressBuffer` arena was created with GPU-read-only `LockBufferForWrite`; commit `b7c2b47d` removes that flag while retaining `Raw | Index`, and the unchanged regression passes in run `32502920948`.
- [x] Attempt a Metal-compatible indexed-indirect arena representation without changing geometry ownership, publication, LOD, or frame budgets. Candidate 1 keeps the contiguous `SurfaceGeometryArena` index payload chunk-local and applies the vertex base only through the indexed draw command, but the standalone Metal frames prove that representation/submission contract is still incorrect in Unity on this path.
- [x] Validate the focused source-level addressing regression on candidate 1 (`32501582037`). The real GPU offset-range EditMode test cannot discriminate under the repository's `-nographics` single-test job: candidate run `32501728417` and clean bucketed control `32501918479` both fail before dispatch with `Kernel 'CSSampleDensity' not found`. Therefore the standalone Metal player is the GPU addressing gate.
- [x] Run candidate 1 (`GetIndirectVertexID_Base` with the corrected GPU-writable arena) through the full standalone-player `CastleScreenshotTests` gate in run `32503126796`; PlayMode and the harness both report success, so the workflow result alone is again insufficient.
- [x] Inspect every candidate-1 PNG against bucketed run `32498578646` and reject candidate 1. The 14.5 s stationary frame is still byte-identical to the clean baseline, but the 24.5-54.5 s survey frames lose/fragment castle geometry and by 44.5/54.5 s contain stretched/scrambled surfaces. The player log has no shader-support error and finishes with `assertion failures 0`, proving the remaining defect is runtime indexed-addressing/submission semantics rather than shader compilation.
- [x] Measure candidate 1 before rejecting it: its stable 35-60 s window averages 346.79 FPS / 2.89 ms versus the correct bucketed baseline's 307.16 FPS / 3.28 ms. The improvement is not accepted because the presented frames are wrong.
- [x] Test the next smallest addressing hypothesis without changing any geometry producer: remove `UnityIndirect.cginc`/`InitIndirectDrawArgs(0)` from the indexed vertex path and consume hardware `SV_VertexID` directly. The focused contract has the intended red (`32504289867`) then green (`32504608289`) proof, so the source-level hypothesis itself is covered.
- [x] Run the direct-`SV_VertexID` candidate through the same five-frame standalone Metal comparison in run `32506084983` and reject it despite the green workflow. The 14.5 s frame remains correct, but the 24.5-54.5 s survey frames lose almost all castle/near-field solid geometry. Removing Unity's command-zero vertex-ID fixup changes the failure mode but does not make one-call indexed multi-command submission Metal-correct.
- [x] Reject the one-call indexed-indirect optimization rather than weakening the visual gate. Both Metal-compatible-looking variants fail presented-frame correctness: UnityIndirect command-zero handling scrambles geometry, while raw hardware `SV_VertexID` drops survey geometry.
- [x] Restore the exact validated bucketed renderer on `rendering-garbled` at `ad1bcc2ac3938534557be2a7bf7abfc62c0856ec`. Comparison with known-good `075d672337e236642c729025bfa89248ccc18081` shows only this investigation plan differs; all rejected indexed-indirect production/test changes are removed.
- [x] Rerun the focused bucketed architecture guard after the restore. `GeometryPipelineArchitectureTests.SolidSurfaceDrawsAreBucketedInsteadOfSubmittedPerChunk` passes in run `32507601077`, and the CI log confirms exactly one test case executed.
- [x] Rerun the full `CastleScreenshotTests` real-player acceptance after the restore in run `32507743110`. Bake, PlayMode, standalone-player capture, artifact upload, and final status all pass.
- [x] Inspect all five restored-run PNGs against baseline `32498578646`. The 14.5 s stationary image is byte-identical (`sha256 b0d484b9d7bc383e847a3902d97c05e4e30540e14d7be772087d8f76609ea263`); all four survey frames retain castle/terrain coverage without giant triangles, stretched surfaces, cross-world corruption, or the disappearing geometry seen in either one-call candidate.
- [x] Inspect the restored player log: no `SmoothSurface`, `SV_DrawID`, shader-support, or indexed-indirect errors are present, and the harness ends after 60 s with `assertion failures 0`.
- [x] Compare the final restored stable 35-60 s window with the original bucketed baseline. Run `32507743110` averages 316.48 FPS / 3.20 ms versus 307.16 FPS / 3.28 ms in run `32498578646`. Because these runs execute the same bucketed production code, the ~3% difference is treated as run-to-run variation, not an optimization win; no incorrect one-call code is retained.
- [x] Identify the next measured rendering hotspot instead of guessing at another submission API. In restored run `32507743110`, scheduler `Prepare` averages 2.12 ms over the stable survey window and its visibility phase alone averages 1.92 ms (~90% of prepare), while admission averages ~0.20 ms and discovery/invalidation are effectively zero. A future optimization should profile/attack visibility/culling selection before revisiting indexed multi-command submission.
- [x] Review the final diff and mark this optimization follow-up complete with the bucketed renderer retained. The attempted one-call optimization has measurable upside but is not Metal-correct under either tested vertex-ID contract. Parent D2/D3 remain independent and open.

## Completion evidence

- Original corrupted real-player run: `32494256445`.
- Rejected `SV_DrawID` real-player run: `32495744003`.
- Clean direct-parent baseline: `32496458837`.
- Clean current-harness/pre-multidraw confirmation: `32497237439`.
- Focused bucketed-submission EditMode guard: `32497707003`.
- Final fixed-branch standalone-player validation / optimization baseline: `32498578646`.
- Indexed-indirect addressing red gate: `32500993168`.
- Candidate-1 addressing contract green: `32501582037`.
- Candidate-1 GPU offset test (non-discriminating `-nographics` failure): `32501728417`.
- Clean bucketed GPU offset control (same `-nographics` failure): `32501918479`.
- Indexed-arena GPU-write contract red: `32502292557`.
- Indexed-arena GPU-write contract green after `b7c2b47d`: `32502920948`.
- Candidate-1 corrected-arena standalone player (visually rejected despite green workflow): `32503126796`.
- Direct hardware-`SV_VertexID` contract red: `32504289867`.
- Direct hardware-`SV_VertexID` contract green: `32504608289`.
- Direct hardware-`SV_VertexID` standalone player (visually rejected despite green workflow): `32506084983`.
- Restored bucketed-submission guard: `32507601077`.
- Restored bucketed standalone-player validation: `32507743110`.
