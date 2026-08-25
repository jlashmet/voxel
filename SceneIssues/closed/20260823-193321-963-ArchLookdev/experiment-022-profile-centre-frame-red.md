# Experiment 022 — retained profile centre-frame red

**Hypothesis** — Retained profile vertices are authored half a voxel away from the continuous
topology because they omit the sample-centre `+0.5` on both radial axes.

**What was performed** — Exposed the pure `ProfilePoint` coordinate helper to the EditMode test
assembly without changing its behavior, and added
`ArchProfileStitchTests.RetainedProfileUsesTheContinuousTopologyVoxelCentreFrame`. The fixture
places a radius-14 point around integer sample centre `(10,20,30)` and requires `(24.5,20.5)` on
the radial axes, matching `TransvoxelTopologyJob`'s documented world conversion. Ran the single
test through `tools/unity-run.sh` on the working tree based at `7e5b34d95`.

**Result** — The test executed 1 case and failed: expected x=24.5, actual x=24.0. Evidence is
`verification-profile-centre-frame-red.txt` and `verification-profile-centre-frame-red.xml`.

**What was learned** — The half-cell authoring mismatch is directly reproduced without rendering
or changing authoritative voxel state. Existing radius tests compared magnitudes but missed the
different coordinate origins.

**Next** — Center retained radial geometry at `centre+0.5` while leaving already face-authored
depth coordinates unchanged. Rerun this regression and then the exact visual replay with prior
silhouette guards neutralized.
