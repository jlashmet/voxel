# Plan — reopened VoxelShowcase surface-transition issue

## Scope

Work only `SceneIssues/open/20260825-032832-253-VoxelShowcase` on `fixes/agent-4`. The earlier agent-4 closure was merged and explicitly reverted because the issue remained visible, so its prior fixes are evidence, not accepted resolution.

## Prior evidence carried forward

- Independent LOD rings can publish overlapping visible geometry. A previous render-staging ownership filter proved that overlap exists, but the exact saved-view replay still showed the reported coarse/striped patches.
- The analytic far terrain is not the source of the three marked regions; the saved view places them well inside its published hole.
- Changing serialized `m_DetailBandScale` from `0.6` to `1.0` moved the first handoff from 57.6 m to 96 m, but retained exact-replay evidence shows the same marked strips. Band distance alone is falsified.
- The previous hierarchy-ownership filter also failed to remove the marked strips. Do not revisit scheduler ownership as the next production change.

## Isolation result

The deterministic transition-boundary regression isolated the visible defect to transition-vertex shading, not overlapping geometry. Transition vertices used a flat face-axis normal while regular Transvoxel vertices followed the density gradient. A first slope-aware implementation exposed a second edge case: clamped central differences at the face snapshot boundary used unequal sample spans and rotated the gradient. The final implementation normalizes each tangential finite difference by its actual clamped sample separation.

## Verification result

- Behavioral regression: green on `c1cfafb7870ee70b48b46eec1e855b988a9f1100` / run `33021302016`.
- Saved-camera real-player replay: assertion, capture, screenshot upload, screenshot-presence gate, and status publication all succeeded in run `33021624676`; the workflow was marked cancelled only after those steps when cleanup exceeded its five-minute job budget.
- Manual inspection of the loaded replay frames and `verification-final.png`: all three marked regions are clean; no coarse transition-shading strip or duplicate coarse surface remains.
- A shorter replay was reissued as run `33022161812` to avoid the workflow-budget overrun; it remained queued while the shared self-hosted runner was unavailable.

## Completion

Record the terminal metadata, preserve `verification-final.png`, set the issue to `pending`, and move the capture from `open/` to `pending/` in a separate bookkeeping commit. Do not move it to `closed/`; closure is coordinator/human-owned.