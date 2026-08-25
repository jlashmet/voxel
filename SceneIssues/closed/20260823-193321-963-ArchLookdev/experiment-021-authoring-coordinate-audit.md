# Experiment 021 — authored coordinate-frame audit

**Hypothesis** — The prior renderer-ownership direction may be masking an authoring alignment
error involving the documented half-cell convention.

**What was performed** — Paused topology-filter implementation and read the complete
`specs/002-world-feature-authoring/contracts/authored-boundary-field.md`, the relevant voxel-surface
architecture proposal, `Assets/Scenes/ArchLookdev.md`, the current primitive/profile emission and
extraction code, and the history of commits `8fdd10a28`, `6ffa60dc0`, and `89de3a2f6`. Compared
the structural annulus's sample/render coordinate frame with retained `ProfileBlock` vertices.

**Result** — The authoritative annulus obeys the documented rule: radial distance zero is at the
same integer sample radius used by occupancy, with no radial half-cell bias. Continuous topology
then converts samples to world-voxel coordinates using `ChunkOriginVoxel + local + 0.5f`.
`ProfilePoint`, however, starts directly from integer `ProfileBlock.Centre` and adds no 0.5 on its
two radial axes. The profile is therefore centered half a voxel away from the authoritative
rendered circle on both radial axes. Its depth span is asymmetric too: the front formula accounts
for the entrance cell face, while `BackQ4=(origin.z+Depth-1)*16+projection` ends at the last cell
centre plus projection rather than the rear cell face plus projection. Existing stitch tests
assert raw radius/depth arithmetic but never compare both representations in one world coordinate
frame.

**What was learned** — The hypothesis was confirmed. This is an authoring/presentation alignment
defect; changing authoritative occupancy or boundary distances would violate the documented
half-cell invariant. Broad renderer triangle suppression is premature and remains unimplemented.

**Next** — Add a direct coordinate-frame regression and test the radial `+0.5` centre correction
with exact profile radii and no silhouette guards. Validate rear-face depth separately only if the
radial correction leaves an oblique rear-edge defect.
