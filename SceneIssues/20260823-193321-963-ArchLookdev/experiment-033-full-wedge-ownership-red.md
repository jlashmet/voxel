# Experiment 033 — full-wedge profile ownership red

**Hypothesis** — The retained profile primitive owns duplicate continuous topology through its
full authored wedge, including the visual joint inset; retained radial side faces, not binary
intrados strips, present the intentional joint.

**What was performed** — Changed the pure ownership fixture to require a matching triangle inside
the joint inset to be owned and added a negative case just outside the raw wedge. No predicate
behavior changed. Ran only this fixture through `tools/unity-run.sh`.

**Result** — The test executed 1 case and failed on the joint-inset triangle. Evidence is
`verification-full-wedge-ownership-red.txt` and `verification-full-wedge-ownership-red.xml`.

**What was learned** — The remaining binary strip is encoded by the current predicate rather than
required by the retained presentation contract. The profile-only replay is the visual evidence
that removing it preserves intentional joints.

**Next** — Assign each matching triangle by centroid within the raw wedge and rerun the focused
fixture and exact camera.
