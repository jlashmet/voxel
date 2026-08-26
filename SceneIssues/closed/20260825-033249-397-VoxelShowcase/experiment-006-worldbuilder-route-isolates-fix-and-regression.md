# Experiment 006 — WorldBuilder route isolates fix and regression

## Hypothesis

The capture-era floating tower comes from VoxelShowcase using the legacy `KentridgeCombinedVoxelCatalogue` composition path. If the later switch to the single WorldBuilder-authored Kentridge path is the responsible source delta, then that switch should line up with the fresh-replay A/B and the repository's boundary regression should prevent the legacy path from returning.

## What I performed

- Compared capture-era source `760dc909138088a46778f026501c17dd25f1b86d` with the current persistent feature branch.
- Isolated commit `416522e1816fd4e6a315f9831e523156304e1c18` (`SceneIssue 040805: route VoxelShowcase through WorldBuilder`) as the behavior-changing composition delta in `Assets/Game/Composition/Showcase/ShowcaseCatalogue.cs`.
- That commit replaces `MountingForce.WorldGen.Voxel` / `KentridgeCombinedVoxelCatalogue.Build(...)` with `WorldBuilderTownAuthoring.Author(...)` plus `WorldBuilderVoxelCatalogue.Build(...)`, so VoxelShowcase realizes the single WorldBuilder-authored Kentridge plan instead of the legacy parallel catalogue.
- Reused the already-recorded exact-pose fresh replay A/B:
  - current-source fresh replay: experiment 001 / Actions run `32886508286`; the tower is absent in every settled frame from `t=24.5s` through `t=84.5s`;
  - capture-era fresh replay: experiment 005 / Actions run `32892084683`; the tower is present throughout settled replay, including frames bracketing the original `222.427658s` capture time.
- Reset `ci-test/fixes/agent-1` to feature head `12fd9c22e5e4aec40432a02b3ef7c04ec8bd8859` and requested the focused regression `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary`.
- Targeted-CI request commit: `8947407043450b4df94fc61830fab140ea6ed41d`; Actions run `32895526009`; status context `ci/single-test`.

## Result

**Confirmed.** The fresh-replay A/B changes exactly with the composition source: the capture-era legacy catalogue emits the unsupported roofed/tower mass, while the WorldBuilder-routed source does not. The isolated production fix is `416522e1816fd4e6a315f9831e523156304e1c18`.

The focused boundary regression passed (`ci/single-test: success`). It explicitly forbids VoxelShowcase from depending on `MountingForce.WorldGen` or containing `KentridgeCombinedVoxelCatalogue`, which protects the causal invariant that removed the captured object instead of encoding a camera-specific pixel workaround.

No additional capture-specific production change is warranted: the minimal deterministic fix already exists on the persistent branch because the later assigned WorldBuilder-consolidation issue corrected the same composition boundary after this capture was recorded.

## What I learned

The defect was not a transient streaming artifact, stale startup bake, or replay mismatch. It was deterministic content from a now-retired parallel Kentridge generation route. The appropriate regression is therefore the single-authoring-boundary invariant rather than a one-off support offset for the captured tower.

## Next

Remove the temporary capture-specific replay workflow, complete the plan and terminal `issue.json` fields, move the entire capture to `SceneIssues/closed/`, verify the remote terminal state, and stop without starting another capture.
