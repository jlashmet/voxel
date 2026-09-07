# Experiment 001 — HouseShowcase debug coverage root cause

## Baseline discriminator
- Feature SHA: `4ceb27cd97c4688273231163c37d6dea7c914b2f`
- Exact targeted-CI request: `e0575bd9619dd6c4ac7f0bd1b0dc426696513953`
- Workflow run: `33994794734`
- Artifact: `9977873261`
- Automatic Showcase module validation: passed.
- Built `Assets/Scenes/HouseShowcase.unity` replay: passed structurally and produced durable screenshots.

The built Wizard-house screenshots reproduce the issue: shell faces are flat cyan/teal and roof/accent faces are bright magenta instead of reading as the authored wood/masonry/cloth/lit-window material presentation. The player log contains no shader, material, texture, unsupported-shader, or rendering exception that explains the colors.

## Competing hypotheses and discriminator result
1. **Production material identity/authoring defect.** Falsified. HouseShowcase registers `GameMaterialComposition.SimulationDefinitions()`, the Kentridge guild-house profile resolves semantic Wood/MasonrySmall/Cloth/LitWindow materials, and the shared material presentation catalogue contains the corresponding rendering definitions.
2. **HouseShowcase environment/presentation defect.** Confirmed and narrowed to diagnostic renderer state. HouseShowcase passes a non-white first argument to `RenderingComposition.ConfigureEnvironment`; that argument is explicitly `surfaceDebugTint`.

## Root cause
`VoxelRenderBridge.SurfaceDebugTint` documents white as production and non-white as a diagnostic surface tint. `VoxelRenderPass` sets shader `_DebugCoverage` to 1 whenever the surface debug tint/base color is not white. `SmoothSurface.shader` checks `_DebugCoverage` before material evaluation and immediately returns encoded surface normals:

`normalize(input.normalNS) * 0.5 + 0.5`

That explains the exact cyan/magenta face colors and why all authored textures appear absent without any shader error: material albedo/normal/surface evaluation is intentionally bypassed by the diagnostic branch.

## Selected fix
Keep ownership scene-local. HouseShowcase now uses `Color.white` as its production surface debug tint. The ordinary VoxelShowcase runtime does not call `ConfigureEnvironment`, so no same-owner sibling needs migration. A Showcase EditMode regression pins HouseShowcase's production debug-tint policy to white; final proof remains the repository-derived module validation plus a new built HouseShowcase screenshot replay showing real material presentation instead of normal-debug colors.
