# GPU handle-command ownership regression (G10 / G12)

## Exact starting point and pending validation

Feature `568a8b8f1251760eb92b6cfd1ef547e2cee4c569`; arena blob `c881cf807874fee060670a867e5dac0e3a12c276`. Fetched remote master `356b2e0e4d2818901c73bbc6b1788f8d6850356d`. Direct git transport is unavailable in this worker; repository refs and source were fetched with the GitHub connector. No master merge is claimed.

Request `fb4a7a92de3420c0affa2a5463287d0252f67797`, run `34007154618`, remains queued at inspection. Do not replace it. Its source remains `9684ff509d65ab7a1caca6245d0f0093f28e249d`, excluding the later timing patch and these tests.

## Observed production-code contracts

`QueueRelease` appends a handle to `_releasedHandles` on every call. `FlushHandleCommands` pushes every entry into `_freeHandles`. Two releases of one acquired handle therefore enqueue the same free handle twice. This is a directly traced host-side ownership defect; an actual Unity regression result is still required.

`QueueCommand` appends independent records for repeated generations of one handle. `CSApplyHandleCommands` runs one GPU thread per record, with no same-handle serialization. Historical source `a0ac0f5e0337911cd67076565baaa50d49b1a0fb` contains command-index coalescing, but it is absent from the current arena. Reconcile that bounded mechanism; do not import its separate publication/fence changes blindly.

A terminal release must not be overwritten by a subsequent generation update before reacquisition. Last-command-wins alone does not preserve this cleanup obligation. A per-handle host ownership state can reject that misuse and make duplicate release idempotent until reacquisition.

## Before-fix regression surface

`Assets/VoxelEngine/Rendering/Tests/EditMode/GpuPageArenaHandleCommandTests.cs` uses the real `GpuSurfacePageArena` and production `GpuSurfacePageArena` compute resource. Seven cases cover duplicate release before/after flush, interleaved generation transport and GPU results, release dominance, unacquired-handle rejection, serial reuse, and transport capacity overflow. Tests inspect the uploaded command buffer as well as actual GPU-written desired generations. No mock shader, source-string assertion or replacement renderer is used. Blocking readback is confined to tests.

This initial commit intentionally leaves production code unchanged so exact fail-before proof can be collected after the existing request terminates. No C# compiler or Unity runtime is available locally; tests have not been compiled or executed here. Predicted failures are not recorded as observed passes/failures.

## Validation ownership and limits

Rendering owns these EditMode tests and the existing `Rendering/Validation/SolidGpu/` minimal/multi-chunk player scenarios. Required module players and full GPU VoxelShowcase replay remain required with the repair; their production lifecycle path exercises handle generation and release. G10/G12 remain unchecked.

This bounded repair does not by itself solve pending-page cleanup, late commands after handle reincarnation, early publication, completion outcomes, source leases, page-table-bank lifetime, GPU-lag reuse or mixed-LOD compatibility. Keep those obligations in the active checklist. No CPU renderer removal or frame-rate result is claimed.
