# Experiment 007 — fallback-contract stale guard

**Hypothesis** — The broader `ShowcaseTraversalFallbackContractTests` remain compatible with the
ground-cover fix.

**What was performed** — Ran the complete five-test EditMode fixture through
`tools/unity-run.sh` on the fixed working tree based at `87bfc27d7`.

**Result** — The hypothesis was inconclusive because four tests passed and one unrelated stale
terrain guard failed. `MovementPrefetchAndNaturalTerrainDoNotReintroduceVisualRegressions` still
requires the former `9/70 + 7/24` valley octaves, but source commit `7c41cd056f` and resolved
SceneIssue 013924 deliberately replaced them with the current gentle `9/18` octave. The failure is
not caused by the low-surface binding edit.

**What was learned** — The broader fixture cannot serve as green evidence until its obsolete
numeric assertion matches the already-authoritative SceneIssue 013924 terrain invariant. Its
continuous-grass assertions are current and remain relevant to this issue.

**Next** — Update only the stale octave assertions to require `9/18` and reject restoration of
`9/70` or `7/24`, then rerun all five tests.
