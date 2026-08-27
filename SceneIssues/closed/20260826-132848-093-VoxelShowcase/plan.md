# Plan — VoxelShowcase water rendering

## Repro / acceptance
The note reports lost water rendering. The sole saved VoxelShowcase pose is `(58.35, 18.35, 0.40)` with no circles, so the full recorded view is the acceptance region.

## Diagnosis
1. **Water content/meshing disappeared.** The Showcase world still registers its water material and the production render pass still contains the water draw path.
2. **Diagnostic presentation state leaked across scene entry.** `VoxelRenderBridge.WaterRenderEnabled` is a sticky static used by `-voxel-disable water`; normal VoxelShowcase entry did not restore it.

The discriminator forced that switch false, then exercised the production Showcase presentation-default path. The Showcase path restored water; an unrelated scene preserved the explicit disable. This selects hypothesis 2.

## Fix / regression
`VoxelShowcasePresentationDefaults` restores water only when `VoxelShowcase` loads. A later explicit diagnostic harness can still disable it, and unrelated scenes are unchanged. `ShowcaseWaterPresentationRegressionTests` protects both behaviors.

## Cost / blast radius
Scene-load only: one persistent subscription and one scene-name comparison. No generation, meshing, shader, storage, or per-frame work changed.

## Verification
Production fix commit: `14c090dea62b50a31993b850472c3f593a8e4d84`.
Exact integrated source tested: `99b8f84f0ed9bb3baac7081a95e0178e132bd8cb`.
CI request `8da01f35f31fc100f23a00b89b1f0d8b88f7efd5`, run `33034841347`: `ci/single-test=success`, 2/2 PlayMode tests passed.
The 45 s real-player replay completed with zero harness assertion failures. The final saved-pose frame visibly restores water across the foreground and is retained as `verification-final.png`.
