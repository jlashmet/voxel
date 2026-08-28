# Experiment 024 — distinct leaf / bouquet ratio

**Observed falsifier.** Exact organic request `bd9ab952d237cf999ca7115301a92699aac44327`, run `33152767543`, passed its focused regression and 45-second real-player replay. Direct inspection still rejects the saved Hero Arch pose: the ivy remains a narrow decorative garland because oversized leaves merge into repeated pointed cards, and each five-head bouquet collapses visually into a few pale circular blobs.

**Discriminator.** Topology, placement, slivers, count, warm hue, and one-shot lifecycle are green. The remaining variable is size-to-spacing ratio: individual leaves/blossoms need to be visibly separable while their combined support footprint remains lush.

**Action.** Keep the same 128 leaves / 30 heads / exact 2,484/77 topology. A final one-shot reference stage reduces leaf silhouette size while widening irregular centre spread, softens the lobe profile, increases dark/light green variation, neutralizes material multiplication, and shrinks each flower head while spreading all five heads within the same bouquet anchor.

**Regression / falsifier.** `ArchReferenceGrowthReferenceFinishPassTests.ReferenceFinishSeparatesLeavesAndBlossomsAcrossRebuild` requires no stem/sliver regression, bounded smaller leaf radius, bushy centre spread, nonzero nearest-leaf separation, visible green variance, small head radius, distinct within-bouquet head separation, warm petals, unchanged 128/30/3-draw/<=4096 budget, and rebuild stability. Reject if the exact saved pose still reads as a garland or icon blobs.

**Blast radius / cost.** ArchLookdev-only construction-time rewrite of existing mesh buffers; one bounded component/coroutine, no new vertices/renderers/draws/per-leaf objects or steady-state work.
