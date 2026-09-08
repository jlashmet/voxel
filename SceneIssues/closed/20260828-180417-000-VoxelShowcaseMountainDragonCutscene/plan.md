# Plan

## Closure override
Repository owner explicitly directed closure on 2026-09-06 despite the remaining rendered near-surface corruption because that defect is owned by the separate GPU renderer restoration SceneIssue, not by Mountain Dragon composition/gameplay.

## Accepted Mountain Dragon evidence
Exact request `981f9f36683aad2b3e0d5e73cd100ec21da7fa9c` / run `34024289067` validated source `f10ce63f128931173947d44b5a7d925a8cec1f15`: repository-derived module validation passed, standalone replay completed all 92/92 waypoints grounded, summit proximity fired, exact dialogue `Hello, I'm Mr. Dragon.` was captured, and matching startup payload/manifest export was proven. The semantic-far slab/error-magenta defect was fixed.

## Renderer ownership
Human review still observed torn/floating near-surface strips/holes. Same-camera isolation and runtime diagnostics attribute that remaining presentation defect to the shared voxel near-surface renderer. It is intentionally not fixed in agent-4 and remains owned by the GPU renderer correctness/restoration work.

## 2026-09-07 master reconciliation
`fixes/agent-4` accumulated unrelated branch history after the accepted Mountain Dragon implementation, so that history must not be promoted wholesale. Rebuild the feature directly on current `origin/master`, carrying only the still-missing Mountain Dragon production code, reusable WorldBuilder/Cutscenes capabilities, focused regressions, module-local player validation, startup-bake provenance, and durable evidence route. Preserve all newer master implementations on overlapping files and exclude Kentridge/GameSystem work, renderer experiments, CI-planner experiments, and every other SceneIssue.

The reconciliation also repairs Unity asset identity defects that were safe only on the polluted branch: reused `.meta` GUIDs are replaced with unique GUIDs, missing `.meta` files are tracked, and the durable evidence-route regression points at the authoritative `SceneIssues/closed/...` path rather than legacy `open/...`.

Revalidate the reconciled exact feature SHA through the assigned `ci-test/fixes/agent-4` transport only after any already queued/running request on that transport reaches a terminal state. Do not replace or cancel existing CI. After green exact-SHA validation and current startup-payload confirmation, refresh closure evidence as needed and promote only through PR + auto-merge.

## Final disposition
Closed by explicit repository-owner waiver. Preserve the remaining renderer defect as an external tracked defect rather than misrepresenting it as fixed here.
