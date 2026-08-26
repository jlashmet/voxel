# Experiment 002 — restore the existing production cutover, not a new mesher

**Question** — Can the performance path be restored without rewriting the renderer or discarding fixes made after the GPU work?

**Method** — Inspected path history for `CpuTransvoxelChunkCache.cs`. The unconditional CPU rollback (`760dc909`) is the most recent commit that touched this production file; no later commit on the assigned branch changed it. Compared that file with its direct pre-rollback parent (`7094d152`), where the exact-ring GPU path was enabled by default and could be disabled once at process start with `VOXEL_DISABLE_GPU_CUTOVER=1`.

**Result** — Restored the pre-rollback production file blob exactly. This removes only the unconditional CPU gate and restores the existing emergency override; it does not broaden GPU eligibility. Steps 1 and 2 remain the only GPU-supported rings, while step 4, block HLOD, unsupported graphics devices, unavailable async counters, GPU-context creation failures, and content rejected by GPU eligibility all retain their existing fallback behavior.

**Interpretation** — This is materially safer than inventing another GPU path: it reactivates code that already has CPU density/topology oracle coverage and shared-arena publication semantics, while retaining an operational CPU escape hatch. The change still requires current Unity CI and Showcase replay evidence before the issue can close.

**CI note** — A dedicated `ci-test/fixes/agent-2` request was written for the red regression before this production change. GitHub recorded the request commits but emitted no workflow/check run from the connector-authored push, so that request is not counted as executed evidence. The source-level regression is deterministically red against the prior hard `true` gate; post-fix CI must still execute successfully before closure.
