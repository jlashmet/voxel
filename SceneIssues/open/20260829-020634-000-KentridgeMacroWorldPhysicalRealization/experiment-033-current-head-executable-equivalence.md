# Experiment 033 — current-head executable equivalence

## Question
Would a new targeted-CI request from the current feature head be materially different from Retry 5, or would it repeat the same executable state and known renderer-baseline failure?

## Comparison
Compared Retry 5 product source `7e6d30858677f2504763e891289293c9507cfd9f` with current feature head `9f6af00d4ce017b2077b68a503d5544f99698c46`.

GitHub reports the current head is nine commits ahead. The only changed files are:
- `experiment-030-throughput-retry5-baseline-renderer-gate.md`
- `experiment-031-retry5-cost-and-master-renderer-prerequisite.md`
- `experiment-032-perimeter-plinth-static-work-cost.md`
- `plan.md`
- `tasks.md`

There are no production, test, workflow, scene, or project-setting changes between the Retry 5 source and the current feature head.

## Result
The current feature head is executable-equivalent to Retry 5 for Kentridge and renderer/module validation. A new CI request based only on the current documentation commits would not constitute a materially different fix and would be expected to encounter the same stale pre-merge renderer module failures before the process-isolated Kentridge regression.

The renderer restoration is available only on current master `b18d470f66221c7cb6091249f4683c2d994bffec`, while the coordinator-prescribed sequence requires green exact-SHA gates before merging current master. Therefore:
- do not issue an identical targeted-CI request from the documentation-only head;
- do not cherry-pick/copy renderer changes into this assignment;
- do not weaken repository-derived validation or the strict publication-coverage evidence gate;
- retain the blocker until the required ordering can be satisfied or coordinator instructions change.

This experiment records blocker equivalence only; it does not validate the unchecked plinth regression, built-player visual acceptance, or final cost gates.
