# Experiment 015 — mass breakup + bouquet zones

**Hypothesis.** Experiment 014 removed every visible stem, but the player still reads a garland because leaf-cluster centres remain nearly uniformly distributed along the full authored path. The same spacing problem makes ten tiny flower clusters read as icons. If the existing topology is gathered into a few semantic zones, the saved pose should gain the reference's negative space and bouquet mass without new render work.

**Action / source.** Add a final one-shot ArchLookdev-only pass after the English-ivy pass. It measures the generated cluster centres, contracts left ivy into lower-pier, upper-pier, and crown zones around those generated centroids, and leaves the sparse right path untouched. It gathers all 30 existing flower heads into three bouquet zones and scales the same petal/centre vertices ~1.3x. Add a production PlayMode regression for zone compactness, two explicit negative-space gaps, bouquet compactness/head radius/depth, budget, mesh identity, and rebuild behavior.

**Discriminator.** Experiment 014 exact run `33142488637` / request SHA `654eead14...` is green, but its real-player `verification-final.png` still shows a continuous diagonal band and repeated tiny five-petal marks. Experiment 015 is accepted only if the same pose has clearly separated foliage masses and rich flower groupings; green metrics alone are insufficient.

**Cost / blast radius.** ArchLookdev only. No topology, renderer, draw-call, or vertex-count increase; one extra one-shot vertex translation/scale mutation and no steady-state work.

**Verdict.** Pending exact-SHA regression + 45-second real-player replay inspection.
