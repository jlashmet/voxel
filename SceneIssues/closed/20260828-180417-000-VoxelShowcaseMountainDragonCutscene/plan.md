# Plan

## Closure override
Repository owner explicitly directed closure on 2026-09-06 despite the remaining rendered near-surface corruption because that defect is owned by separate shared-renderer work, not Mountain Dragon composition/gameplay.

## Accepted Mountain Dragon evidence
Exact request `981f9f36683aad2b3e0d5e73cd100ec21da7fa9c` / run `34024289067` validated source `f10ce63f128931173947d44b5a7d925a8cec1f15`: repository-derived module validation passed, standalone replay completed all 92/92 waypoints grounded, summit proximity fired, exact dialogue `Hello, I'm Mr. Dragon.` was captured, and matching startup payload/manifest export was proven.

## Renderer ownership
Human review still observed torn/floating near-surface strips/holes. Same-camera isolation attributed that remaining presentation defect to the shared voxel near-surface renderer. It remains outside agent-4 ownership under the explicit repository-owner waiver.

## Master reconciliation
The prior branch accumulated unrelated history and must not be promoted wholesale. Rebuild directly on current `origin/master`, carrying only still-missing Mountain Dragon production code, reusable WorldBuilder/Cutscenes capabilities, focused regressions, module-local player validation, startup-bake provenance, and durable evidence. Preserve newer master implementations and exclude Kentridge/GameSystem work, renderer experiments, CI-planner experiments, and every other SceneIssue.

Reconciliation repairs reused/missing Unity `.meta` identities and updates the durable evidence-route regression to the authoritative `SceneIssues/closed/...` path. The late planner change `a67758fa610c2b99782b338809f3cc25a0f9726d` remains excluded because private implementation changes in an API assembly can alter behavior observed by unchanged dependents; suppressing dependent validation would weaken the fail-closed gate.

## Obsolete-source compile discriminator
Old request `8c4cee6db71f50ca5e87e200308b079b2b2aa421` / run `34175607847` finally ran against obsolete polluted source `f1c1f975d6282858d0f104e42a075017d29d3ead` and failed compilation in `WorldBuilderMountainLandmarkMaterialCatalogue.cs`. That legacy catalogue referenced removed `MountainLandmarkSpec.PathTier`, `PathHeadroomVoxels`, `PathClearanceWidthVoxels`, and `MountainPath*Geometry` contracts. The accepted Mountain Dragon implementation no longer consumes that catalogue; it uses `MountainLandformSurface` plus `WorldBuilderMountainLandformCatalogue` and the shared road path. Remove the superseded catalogue and its `.meta` rather than resurrecting obsolete contracts.

Corrected source commit: `d3463335b2ecf95d21f94b7b9da0b95b8e3f4928`.

Next gate: exact-SHA targeted CI from the corrected feature source, then consume the exact generated startup bake/manifest via `tools/binary_transport`, revalidate the payload-bearing source, and promote only through a new PR + auto-merge.

## Final disposition
Closed by explicit repository-owner waiver. Preserve the remaining renderer defect as external tracked work rather than misrepresenting it as fixed here.
