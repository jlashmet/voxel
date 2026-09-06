# Plan

## Closure override
Repository owner explicitly directed closure on 2026-09-06 despite the remaining rendered near-surface corruption because that defect is owned by the separate GPU renderer restoration SceneIssue, not by Mountain Dragon composition/gameplay.

## Accepted Mountain Dragon evidence
Exact request `981f9f36683aad2b3e0d5e73cd100ec21da7fa9c` / run `34024289067` validated source `f10ce63f128931173947d44b5a7d925a8cec1f15`: repository-derived module validation passed, standalone replay completed all 92/92 waypoints grounded, summit proximity fired, exact dialogue `Hello, I'm Mr. Dragon.` was captured, and matching startup payload/manifest export was proven. The semantic-far slab/error-magenta defect was fixed.

## Renderer ownership
Human review still observed torn/floating near-surface strips/holes. Same-camera isolation and runtime diagnostics attribute that remaining presentation defect to the shared voxel near-surface renderer. It is intentionally not fixed in agent-4 and remains owned by `20260902-171853-000-GpuRendererProductionRestoration`.

## Final disposition
Close this SceneIssue by explicit repository-owner waiver. Preserve the remaining renderer defect as an external tracked defect rather than misrepresenting it as fixed here. Promote only through the normal `fixes/agent-4` PR path; do not push directly to `master`.
