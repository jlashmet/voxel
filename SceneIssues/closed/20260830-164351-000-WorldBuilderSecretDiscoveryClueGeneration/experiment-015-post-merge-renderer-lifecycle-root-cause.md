# Experiment 015 — post-merge renderer lifecycle root cause

## Trigger

Required convention-derived validation was retried from exact feature source `731e55c2ed1808c2d6fb21189c96011887bec6e3` through the only allowed CI transport. Request commit `33d0b9a7f20512150024c7994b9cfdeb004b5856` produced run `33716327931`.

The optional requested-test path was omitted because experiment 014 already proved that path is broken infrastructure. WorldBuilder EditMode validation still discovered/passed the full 918-case assembly. The standalone SceneIssue replay also completed successfully.

## Competing hypotheses

1. The CPU renderer fallback merged from current master still fails to publish near-surface voxel geometry, so the prior blank-sky visual symptom persists.
2. Near-surface publication is restored, but the required built-player gate is failing for a separate renderer lifecycle defect.
3. Secret/cave generation regressed and the production validation no longer creates the clue/breach evidence.

## Evidence

The built player logged the same semantic product evidence as the prior green proof:

- `caveSegments=30`, `branches=4`, `terminals=5`, `clueAnchors=2`
- `crackVoxels=35`
- authored barrier `int3(-879, 169, 534)->int3(-876, 189, 546)`
- production destruction removed `607` voxels and exposed the expected hidden-pocket bounds.

Full-resolution captures falsify hypothesis 1. The 9/12/15 second frames now contain the rendered cave boundary and a clearly visible sparse branching dark fracture. The 18/21 second frames contain the breached/opened interior. This is materially different from run `33715584697`, whose corresponding frames contained only sky/floating vegetation and no near-surface voxel world. The current-master CPU fallback therefore restored near-surface publication.

The player then fails during teardown, after all eight requested captures and after the wall-destroyed proof. `WorldBuilderSecretDiscoveryValidation.OnDisable` calls the shared semantic cleanup `RenderingComposition.ClearWorld()`. The first managed failure is a `NullReferenceException` in `GpuSurfaceMirrorCoordinator.DetachPageArena` reached from renderer world-resource release. Shutdown then segfaults while disposing CPU surface resources (`TransvoxelBuildWorkspace.Dispose` -> `CpuTransvoxelChunkCache.Dispose` -> `VoxelSurfaceScheduler.Dispose` -> `VoxelRenderPass.Dispose` -> URP renderer disposal).

This isolates hypothesis 2. Secret planning/authoring/destruction is not the failing phase, and the previous no-surface visual symptom did not recur. The failing gate is a renderer resource-lifecycle defect introduced/exposed by the current master rendering state.

## Scope decision

Do not patch renderer ownership/lifecycle from this WorldBuilder assignment. `RenderingComposition.ClearWorld()` is the correct shared semantic cleanup boundary for an application-owned world and deliberately releases renderer-derived storage pins before the world is disposed. Skipping it only in this validation scene would hide a production teardown fault and weaken the acceptance requirement that the built application run without runtime exceptions.

Treat the renderer teardown failure as an external prerequisite blocker. No third renderer/camera workaround is allowed from this assignment. Independent WorldBuilder work remains valid; exact merged-SHA closure remains blocked until the renderer lifecycle defect is corrected externally and the required player gate is rerun.

The separate unresolved acceptance conflict also remains: `issue.json` requires representative secret examples in `WorldbuildingGalleryShowcase`, while explicit user direction prohibited this assignment from integrating the feature there. Acceptance is unchanged.
