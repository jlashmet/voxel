# Quality review — 2026-08-29

The current `fixes/agent-3` bookkeeping must **not** be promoted or merged as a completed SceneIssue yet.

- The branch moved this assignment to `pending` while its own `resolutionSummary` says it is still **awaiting exact-SHA CI and rendered acceptance evidence**. That contradicts the SceneIssues workflow: `pending` is only appropriate after the required exact-SHA targeted CI and exact-SHA built-application acceptance gate are green.
- Acceptance criterion (12) requires the exact built application to **traverse from the surface into the cavern and to the ruin** and capture the entrance, descent, cavern reveal, formations, ruin/statues, and lighting. A staged camera sequence or camera retargeting is not a substitute for production-gameplay traversal of the full route.
- Do not narrow or retarget the evidence harness merely to make the gate pass. The harness must prove the authored experience using normal production movement/traversal and the original acceptance criteria.

Required before promotion: keep/revert the assignment to `open`, obtain green exact-SHA targeted CI, obtain green exact-SHA built-player traversal/rendered evidence for the complete route and required visual stages, inspect that evidence against the AAA world-authoring bar, and only then set pending/closure metadata.
