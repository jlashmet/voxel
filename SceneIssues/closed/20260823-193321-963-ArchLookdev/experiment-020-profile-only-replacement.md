# Experiment 020 — retained-profile-only replacement

**Hypothesis** — Retained profile blocks alone form a complete smooth hero-ring surface, so the
duplicate binary topology can be replaced rather than covered with larger offsets.

**What was performed** — In the structural-only reproduction, temporarily skipped both compacted
continuous and faceted result streams for chunks containing retained profile blocks. Profile
emission still validated against the authoritative snapshot and ran normally; lower non-profile
chunks were unaffected. Rebuilt through `tools/unity-run.sh` and ran the exact 1637x1140 camera for
25 seconds on the working tree based at `7e5b34d95`. Evidence is
`verification-profile-only-pose.png`, `verification-profile-only-marked-region.png`, and
`verification-profile-only-build.txt`.

**Result** — The hypothesis was confirmed. The retained ring is smooth throughout every marked
inner-curve region and preserves the intentional radial voussoir joints. The staircase is absent.
Piers and unrelated structural areas disappear where the deliberately broad diagnostic skipped
their binary streams, so whole-chunk suppression is not a production solution.

**What was learned** — The retained geometry is a valid smooth replacement. The production defect
is duplicate continuous radial topology remaining underneath it. The smallest safe direction is
triangle-level suppression constrained to matching profile material, depth, angular span, and
inner/outer radial bands; unrelated faceted and continuous geometry must remain.

**Next** — Restore normal authoring and result append. Add a focused replacement-ownership
regression, implement selective covered-topology filtering, then replay the faithful minimum and
the original full scene.
