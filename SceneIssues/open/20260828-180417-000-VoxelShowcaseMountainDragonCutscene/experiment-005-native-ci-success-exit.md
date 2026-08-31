# Experiment 005 — native CI success exit

## Question
Does the exact successful GitHub Actions showcase bake need a process-level POSIX exit rather than a managed Unity/.NET exit to satisfy the unchanged 240 s watchdog?

## Evidence
- Exact feature source `3635cc2ee657...` changed only the already-gated successful-bake delegate from `EditorApplication.Exit` to `Environment.Exit`.
- Exact transport request `90e824415962...` produced run `33310154810`; it was not replaced while queued/running.
- The bake step ran from `11:56:09Z` to `12:00:10Z` and failed at the 240 s guard; the focused test was therefore skipped, while the independent built-player capture succeeded.
- The run artifact's `showcase-bake.log` reaches successful import/save and logs `Baked Voxel Showcase startup world: 200 regions, 18.2 MiB ... content signature 0x7C8A5152`, then ends immediately. Unlike the prior `EditorApplication.Exit` run it contains no `Application is exiting`, domain unload, or graceful cleanup output.
- `tools/unity-run.sh` watches the native Unity PID with `kill -0` and only returns after that PID disappears; otherwise it kills the process session after 240 s.

## Discriminator
`Environment.Exit(0)` stops managed logging but does not make the native Unity editor PID disappear quickly enough. The remaining failure is process termination, not world generation, sparse structure semantics, persistence, or built-player traversal.

## Selected change
Keep the existing exact policy gate and ordering, but make its production completion action call POSIX `_exit(0)` on macOS/Linux after world disposal, payload/manifest writes, AssetDatabase import/save, and success logging. Retain a managed fallback for non-POSIX editor hosts. The injectable completion action remains the policy-test seam, so ordinary editor/test/runtime contexts never invoke the native exit.

## Blast radius / cost
Editor-only, exact CI bake success path only. No workflow limit changes, no runtime/player code, no voxel generation, no movement/collision, no structure program, and no startup coverage reduction. Expected performance effect is only removal of post-success native Unity teardown latency; bake generation/memory cost is unchanged.
