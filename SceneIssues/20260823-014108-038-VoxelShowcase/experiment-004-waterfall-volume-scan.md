# Experiment 004 — waterfall volume scan

**Hypothesis** — The failed single-voxel samples result from plan drift or wholesale loss of water
during asynchronous castle publication.

**What was performed** — Updated the working-tree direct regression to read the loaded world's
private authoritative castle plan, compare it with the reconstructed plan, and scan the expected
waterfall volume for Water and Cascade materials. Ran the PlayMode test through
`tools/unity-run.sh`; evidence is in `verification-waterfall-volume-scan.xml`.

**Result** — The hypothesis was disproven. Loaded and reconstructed plans match exactly. The scan
found 7,282 sampled Water cells spanning Z 36..652, including 16 sampled Water cells across the
chosen upper-stream slice. It also found 689 sampled Cascade cells at the waterfall lip. The test
still failed because one presumed centre voxel was Empty.

**What was learned** — Water and cascade publication is intact; the defect is not missing authored
state. A single centre-coordinate assertion is too brittle for the shaped stream cross-section.
The stable invariant is non-empty Water/Cascade volume in the authored slices plus empty ravine
lanes around the fall.

**Next** — Replace the diagnostic full-volume scan and brittle centre samples with bounded
slice-volume assertions and the three direct clearance assertions, then rerun the focused test.
