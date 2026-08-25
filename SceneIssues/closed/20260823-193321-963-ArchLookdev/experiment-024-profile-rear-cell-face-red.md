# Experiment 024 — retained profile rear cell-face red

**Hypothesis** — The retained profile ends one voxel before the rear structural face. Occupied
samples span `origin.z` through `origin.z+Depth-1` and are presented at sample centres `z+0.5`, so
the rear cell face is `origin.z+Depth`. A symmetric positive projection must start from that face,
not from the last occupied sample coordinate.

**What was performed** — Updated only the existing
`ArchProfileStitchTests.RetainedProfilesSpanFullArchDepthAndMatchStructuralAnnulusZeroes`
expectation from `(Depth-1)*16 + projectionQ4` to `Depth*16 + projectionQ4`. Ran that single
EditMode test through `tools/unity-run.sh` on the clean production baseline at `7e5b34d95`.

**Result** — The test executed 1 case and failed: expected rear Q4=200, actual=184, an exact
16-Q4/one-voxel shortfall. Evidence is `verification-profile-rear-face-red.txt` and
`verification-profile-rear-face-red.xml`.

**What was learned** — Unlike the disproven radial-centre shift, this mismatch is on the exact
depth boundary visible in the marked crop: the smooth retained soffit stops before the binary rear
opening whose staircase remains exposed. The old test encoded the implementation rather than the
structural cell-face invariant.

**Next** — Author `BackQ4` from `origin.z+Depth`, rerun the focused test, then perform an exact-pose
visual replay before expanding test scope.
