# Experiment 11 — fine-band expansion does not remove the marked artifact

## Hypothesis

If the three marked patches are caused by the first step-1 to step-2 LOD handoff, expanding the serialized fine-detail band from 57.6 m to 96 m should materially change or remove those patches in the exact saved-camera replay.

## Performed

Compared the standalone exact-replay evidence from the two prior agent-4 runs:

- hierarchy-ownership replay artifact `9580374989` from Actions run `32892693260`, with the original serialized `m_DetailBandScale = 0.6`;
- detail-band replay artifact `9594146911` from Actions run `32933454625`, after changing `m_DetailBandScale` to `1.0`.

Inspected the final stationary 1364x836 screenshots at the saved camera, including all three reported circles. Also computed a direct per-pixel RGB comparison between the two final frames and estimated view-ray distances for the marked circle centers from the saved camera transform to test whether the marks necessarily lie outside the expanded fine band.

## Source commit / evidence

This experiment is evidence-only on the reopened branch. It compares the retained artifacts from the previous agent-4 investigation whose closure was later merged and explicitly reverted because the SceneIssue remained visible on `master`.

## Result

**Falsified.** The same thin bright/dark contour strips and serrated terrain bands remain in the same marked regions after expanding the fine-detail band. The two final screenshots are effectively identical: mean absolute RGB difference is below 1 intensity level per channel on a 0-255 scale (approximately 0.68, 0.77, 0.58).

The saved-camera ray geometry also does not support treating the marked regions as uniquely outside the fine band. Depending on the terrain intersection height, the marked centers plausibly project into the step-1 region even before the expansion.

## Learned

Changing `m_DetailBandScale` relocates LOD policy boundaries but does not remove the reported visual artifact. The previous green policy test and prior visual judgment were insufficient; the explicit post-merge rejection is consistent with the retained replay evidence.

The earlier render-staging hierarchy filter is also not a sufficient explanation: it removed independently submitted coarse/fine overlap but the exact replay still contained the marked strips. The remaining artifact is therefore already present in geometry/material presentation that survives both ownership filtering and fine-band expansion.

## Next

Per the post-three-attempt workflow rule, do not make another production LOD-policy change yet. Build a minimal isolated regression around the surface extraction boundary path, starting with Transvoxel transition-face geometry and surface attributes. Determine whether transition cells overlap regular boundary cells or carry discontinuous normals/material attributes. Only then apply the smallest production fix tied to a reproduced invariant.
