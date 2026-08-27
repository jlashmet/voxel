# Plan — VoxelShowcase water rendering

## Repro / affected region
The capture note says the water rendering was lost. There is one saved VoxelShowcase camera pose at `(58.35, 18.35, 0.40)` with no circles, so the full recorded view is the acceptance region. Repository binary access does not expose the PNG payload to this worker, so visual acceptance will be performed by exact saved-pose replay and the resulting player-capture artifact.

## Hypotheses and discriminator
1. **Water content/meshing disappeared.** The Showcase world still registers its water material and the production render pass still contains the water draw path; existing water meshing tests remain present. This does not explain a presentation-only disappearance.
2. **The diagnostic water switch leaked across scene state.** `VoxelRenderBridge.WaterRenderEnabled` is a mutable static introduced for `-voxel-disable water`. `ShowcasePlayerHarness` can set it false. VoxelShowcase already restores other sticky renderer settings on enable, but does not restore water. This directly explains an otherwise intact water pipeline rendering nothing.

Smallest discriminator: force the switch false, execute the production VoxelShowcase presentation-default path, and require it to become true; verify an unrelated scene does not clear an explicit diagnostic disable.

## Fix / regression
Restore water only when the production `VoxelShowcase` scene loads, before presented frames. A later explicit diagnostic harness can still disable it for that run. `ShowcaseWaterPresentationRegressionTests` protects both the restoration and the non-Showcase diagnostic behavior.

## Blast radius / cost
Showcase-only scene-load callback; no generation, meshing, shader, storage, or per-frame path changes. One static scene-loaded subscription and one string comparison per scene load.

## Verification
- Target exact regression on `ci-test/fixes/agent-6` from the final feature SHA.
- Replay the sole saved camera pose through SceneIssue CI and inspect/retain the real-player screenshot as `verification-final.png`.
- Confirm no unrelated capture or workflow changes before terminal bookkeeping.
