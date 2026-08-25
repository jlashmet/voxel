# Experiment 031 — selective retained-profile replacement visual attempt 1

**Hypothesis** — Omitting continuous triangles wholly covered by the matching retained profile,
while extending/closing that profile and preserving joint-boundary triangles, removes the marked
duplicate staircase without removing unrelated geometry.

**What was performed** — Applied the green generic ownership predicate only while appending the
continuous topology stream. Added an explicit occupied backing coordinate, extended the retained
geometry through the rear cell face plus projection, and emitted its rear annular face. The full
`ArchProfileStitchTests` fixture passed 5/5. Rebuilt the production player and ran the exact
1637x1140 camera for 25 seconds on the working tree based at `7e5b34d95`.

**Result** — The marked staircase remains. Unrelated geometry and retained profiles are present,
but the image alone does not show whether zero triangles matched or whether the remaining pixels
belong to boundary-crossing/faceted triangles. Evidence is `verification-profile-replacement-focused.txt`,
`verification-profile-replacement-focused.xml`, `verification-profile-replacement-build.txt`,
`verification-profile-replacement-pose-attempt1.png`, and
`verification-profile-replacement-marked-region-attempt1.png`.

**What was learned** — The pure predicate test is insufficient evidence that the live topology
stream exercised the ownership rule. The next diagnostic must count tested and omitted production
triangles before changing coverage or filtering another stream.

**Next** — Instrument production topology append counts for profile chunks and replay the exact
scene. If omissions are zero, inspect live coordinate/material values; if omissions are nonzero,
isolate the remaining stream or boundary triangles.
