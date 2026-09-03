# Disable automatic PlayMode CI

## Goal

Remove broad PlayMode test execution from automatic pull-request and master test workflows. Keep affected/all EditMode tests as the fast code-level validation layer, and keep the existing standalone player-build workflow as the repository-wide high-level validation path.

## Scope

- Change `.github/workflows/tests-pr.yml` to select and run EditMode assemblies only.
- Change `.github/workflows/tests-master.yml` to discover and run EditMode assemblies only.
- Remove automatic `VoxelEngine.Tests.PlayMode` isolated shard execution and showcase baking tied to that suite.
- Preserve `showcase-performance.yml`, which builds and launches the standalone player on relevant master changes.
- Preserve `tests-single.yml` and other manually/targeted invoked validation so agents can still request narrow evidence when a ticket specifically requires it.

## Non-goals

- Do not delete PlayMode test source files.
- Do not change module ownership or test-selection graph behavior for EditMode.
- Do not change standalone-player build/capture implementation.
- Do not alter architecture/static gates.

## Validation

- Inspect the final workflow diff and verify neither automatic test workflow invokes `-testPlatform PlayMode`, supplies PlayMode assemblies to the persistent runner, or runs `VoxelEngine.Tests.PlayMode` shards.
- Verify `showcase-performance.yml` remains unchanged and still contains the standalone player build/measurement job.
- Open a PR against `master` so GitHub validates the edited workflow files on the branch.
