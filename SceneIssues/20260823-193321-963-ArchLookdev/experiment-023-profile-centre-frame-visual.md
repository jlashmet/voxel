# Experiment 023 — retained profile centre-frame visual replay

**Hypothesis** — Moving retained profile vertices by `+0.5` on both radial axes aligns them with
the continuous-topology presentation frame and removes the marked staircase.

**What was performed** — Changed only `ProfilePoint`'s radial centre from the integer primitive
centre to `centre+0.5`; the depth coordinate remained unchanged. Neutralized the earlier silhouette
guards so they could not mask the result. The direct coordinate fixture from experiment 022 then
passed 1/1. Built `ArchLookdev` through `tools/unity-run.sh` and ran the production player at the
exact saved 1637x1140 camera for 25 seconds on the working tree based at `7e5b34d95`.

**Result** — The direct fixture was green, but the exact marked-region replay retained the same
horizontal/vertical staircase. Evidence is `verification-profile-centre-frame-green.txt`,
`verification-profile-centre-frame-green.xml`, `verification-profile-centre-fix-build.txt`,
`verification-profile-centre-fix-pose.png`, and
`verification-profile-centre-fix-marked-region.png`.

**What was learned** — Matching two coordinate formulas syntactically did not prove the authored
profile should move: the exact production image disproves that change as the scene fix. The
retained profile's placement is not the owner of the visible teeth. This experiment and its test
were reverted; the evidence remains as a failed hypothesis. Production profile chunks currently
use CPU topology because profile blocks and planar masonry are outside the partial GPU cutover,
but both CPU and GPU density/topology implementations intentionally share the same boundary math.

**Next** — Keep the authored field and profile emitter at the clean baseline. Instrument the
faithful structural-only reproduction to identify the exact triangles/pixels at the teeth and
compare those triangles to the profile angular/depth coverage. Do not add an arch-specific
CPU/GPU branch.
