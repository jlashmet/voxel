# GPU handle-command ownership regression (G10 / G12)

## Exact source identities

Starting feature `568a8b8f1251760eb92b6cfd1ef547e2cee4c569`; original arena blob `c881cf807874fee060670a867e5dac0e3a12c276`. Fetched remote master `356b2e0e4d2818901c73bbc6b1788f8d6850356d`. Direct git transport is unavailable; repository refs and source were fetched with the GitHub connector. No master merge is claimed.

The test-only before-fix source is **`03498b9bf7bf2bf0bdeee341ee8d08a0ef347dce`**. Its production arena remains unchanged. The repair is its direct descendant; keep both identities for exact fail-before/pass-after validation.

Request `fb4a7a92de3420c0affa2a5463287d0252f67797`, run `34007154618`, remains queued at inspection. Do not replace it. Its source is `9684ff509d65ab7a1caca6245d0f0093f28e249d`, excluding the subsequent timing patch, handle tests and repair.

## Observed production-code contracts

Original `QueueRelease` appends a handle to `_releasedHandles` on every call. `FlushHandleCommands` pushes every entry into `_freeHandles`. Two releases of one acquired handle therefore enqueue the same free handle twice. This is a directly traced host-side ownership defect, not yet an observed Unity regression result.

Original `QueueCommand` appends independent records for repeated generations of one handle. `CSApplyHandleCommands` runs one GPU thread per record, with no same-handle serialization. Historical source `a0ac0f5e0337911cd67076565baaa50d49b1a0fb` contains command-index coalescing, but it is absent from the current arena. Only that bounded mechanism is reconciled; its separate publication/fence changes are not imported.

## Candidate repair

The arena now records one command per handle per flush and a byte-sized `Free/Acquired/ReleaseQueued` host ownership state. Multiple generation updates coalesce in CPU call order. Release is terminal until reacquisition: duplicate release is ignored, generation after release is rejected, and each flushed release returns the handle once. This preserves cleanup instead of blindly applying last-command-wins across release.

The command map is preallocated to the existing 1,024-record transport bound; ownership costs one byte per handle. There is no added production readback, GPU buffer, shader, wait, renderer, geometry path or CPU fallback. Existing GPU lifetime/reuse policy is deliberately not claimed fixed by host bookkeeping.

## Behavioral regression surface and status

`Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPageArenaHandleCommandTests.cs` uses the real arena and production compute resource. Seven cases cover duplicate release before/after flush, interleaved generation transport and GPU results, release dominance, unacquired-handle rejection, 32 serial reuse cycles, and a 1,030-handle transport-overflow/reacquisition case. Tests inspect the real uploaded command buffer and actual GPU-written desired generations. No mock shader, source-string assertion or replacement renderer is used. Blocking readback is confined to tests.

No C# compiler or Unity runtime is available locally. The new Unity tests are not yet compiled/executed; static review and committed code are not substituted for exact-SHA proof. After the active request terminates, collect the before-fix result and candidate result through the same CI transport, retaining GPU module players and full VoxelShowcase replay as required. Do not overwrite active requests to include this descendant.

## Validation ownership and limits

Rendering owns the EditMode tests and existing `Rendering/Validation/SolidGpu/` minimal/multi-chunk scenarios. Required module players and full GPU VoxelShowcase replay still apply; the production lifecycle consumer exercises handle generation/release. G10/G12 remain unchecked.

This repair does not solve pending-page cleanup, late commands after handle reincarnation, early publication, completion outcomes, source leases, page-table-bank lifetime, GPU-lag reuse or mixed-LOD compatibility. Those obligations remain in the active checklist. No CPU renderer removal, final GPU image or frame-rate result is claimed.
