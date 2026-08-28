# Experiment 010 — restore lush reference mass

## Hypothesis
The remaining visual failure is not placement or lifecycle. Run `33132525932` proves the authored hero root renders at the captured Hero Arch pose, but direct comparison with `References/arch_reference.png` shows a thin vine with sparse flat leaves and tiny triangular flower marks. The final detail pass is compressing the base authoring: most leaves are scaled to 0.62–0.82 of their already-authored size and two of five petals are collapsed per flower head.

## Action / source
Baseline source `572592d64609eb5e7042a88ad628864953d2ba89` (current master already merged). Keep the 128 leaves, 30 flower heads, world-space lifecycle, three combined hero draws and <=4,096 vertices unchanged. Add one bounded post-detail mesh correction that restores broad overlapping leaf scale and all five irregular petals, with mostly warm-white blossoms like the reference.

## Falsifier
Reject this hypothesis if the exact saved-pose standalone replay still reads as a thin wire/stamp pattern rather than a layered leafy mass with clearly readable flower clusters.

## Cost / blast radius
ArchLookdev only. Existing mesh buffers are mutated once after construction/rebuild; no new renderer, GameObject-per-leaf, draw call, vertex, or steady-state `Update` cost.