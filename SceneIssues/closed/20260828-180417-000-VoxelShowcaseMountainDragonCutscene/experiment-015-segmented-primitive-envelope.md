# Experiment 015 — segmented primitive envelope discriminator

Exact focused run `33358709551` on source `220eac9861c59f76817b6bcf7efc90464e5734ca` passed the corrected centered-lane width, authored segmented carve count, carve-volume bound, and no-tall-retaining-wall checks, then failed only the historical fixed primitive cap: expected `<= 80`, actual `98`.

This is not a recurrence of the 16-vs-33 headroom defect. The current six-tier route deterministically has 24 shell-following segments. The baseline landform's 98 primitives decompose as:

- 4 core/shoulder frusta (1 tapered core + 3 asymmetric shoulders)
- 40 bounded natural-support frusta (38 from route segments + 1 final ascent + 1 summit approach)
- 26 carve boxes (`sum(SegmentCount) + 2` = 24 + final ascent + summit approach)
- 28 walking-surface primitives (24 segment ramps + entry floor + final landing + final ramp + summit approach)

Total: `4 + 40 + 26 + 28 = 98`.

The old `80` cap predates shell-following segmentation, so it cannot distinguish accidental primitive inflation from the authored segment count. Preserve the shared `FeatureBudget.MaxPrimitivesPerInstance` gate unchanged and replace the fixed local cap with a topology-derived envelope. A conservative authored envelope is `4 * sum(SegmentCount) + 12`: core/shoulders (4), at most two support frusta per route segment plus two terminal supports, one carve per segment plus two terminal carves, and one ramp per segment plus four terminal/entry path primitives. With 24 segments this is 108, so the observed 98 remains below the derived bound while any unbounded per-segment growth still fails.
