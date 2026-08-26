# Experiment 002 — WorldBuilder boundary regression

## Hypothesis

Kentridge currently has more than one externally visible town-authoring path: VoxelShowcase reaches the MountingForce voxel backend directly, while the campaign bootstrap publicly accepts the backend `SettlementPlan`. A regression that requires a WorldBuilder town facade, forbids the legacy backend from VoxelShowcase's assembly references, and forbids legacy planning types from the public Kentridge bootstrap should fail before the fix.

## Regression

Added `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` at feature commit `04f035153598073a105a947f4d4e5bfc8136151f`.

The test verifies:

1. `Game.WorldBuilder.Runtime.WorldBuilderTownAuthoring` exists.
2. `Game.Composition.Showcase.asmdef` contains no `MountingForce.WorldGen` dependency.
3. Public `KentridgeCampaignSessionBootstrap.Plan` parameters contain no type from a `MountingForce.WorldGen*` namespace.

## Targeted CI evidence

- CI branch: `ci-test/fixes/agent-1`
- request commit: `b50735891bebe7862e51c18ab2fa69158cb66fc8`
- request id: `20260825-0942-agent1-kentridge-boundary-red`
- workflow run: `32874662064`
- artifact: `9573437286` (`single-test-32874662064`)
- result: `ci/single-test = failure`

Unity compiled successfully and executed the requested test. The first intended assertion failed:

```text
Town authoring must enter through Game.WorldBuilder.Runtime rather than a content-specific generator.
Expected: not null
But was: null
```

The same run's Unity import log identifies `Packages/com.mountingforce.worldgen` as an embedded package, confirming that the legacy world-generation implementation is still physically owned by `Packages/` before the migration.

## Conclusion

Confirmed. The split boundary is reproducible in CI and the regression is red for the intended architectural reason. Implement the WorldBuilder facade/voxel adapter, migrate VoxelShowcase and campaign entry points, then relocate the embedded backend under `Assets/Game/WorldBuilder/Backends/` while preserving its source and `.meta` identities.
