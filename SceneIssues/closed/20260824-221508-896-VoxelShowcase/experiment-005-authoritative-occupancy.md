# Experiment 005 — authoritative occupancy through mixed density

**Hypothesis.** The residual stall/border gaps occur when mixed continuous extraction treats density-lattice presentation material as exact occupancy. An air-centred sample can carry a nearby Rounded/Smooth solid material and incorrectly suppress an adjacent Planar face.

**Action / evidence.** Historical red request `36b642265e4d41f2444d449530acf58b24d52c22` (run `33016154754`) executed all three ownership tests: both behavioral mask tests passed and only the mixed-path source guard failed. Source inspection then found the matching runtime contract in `TransvoxelTopologyJob`: dominant render material may be solid at an AIR-centred negative-density sample. Fresh-bake experiment 003 had already ruled down stale content and showed the earlier boundary/write fixes were incomplete.

At implementation head `7a8df39059ac10a844207ce75577d9389026a69f`, the density lattice carries authoritative centre occupancy in transient surface flag bit 2, which storage never persists. `FacetedMaskJob` uses that bit for exact exposure; faceted/topology packers strip it before vertex output. The new behavioral regression constructs Planar occupied backing + authoritative air + adjacent Rounded solid and runs `TransvoxelDensityJob -> FacetedMaskJob`.

**Cost / blast radius.** No allocation, no additional Storage read, no authoritative-data mutation. One occupancy bit write per CPU exact density sample and bit tests/masks in faceted publication. Snapshot-only faceted extraction, GPU extraction, and mip rings are unchanged.

**Verdict.** This is the smallest fix matching both captured runtime evidence and the renderer's existing material/occupancy contract.

**Next.** Run one final exact-SHA PlayMode request with the behavioral test and SceneIssue replay; inspect all four original circles before pending promotion.
