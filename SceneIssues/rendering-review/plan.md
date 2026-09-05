# Rendering architecture decision — evidence first

## Recommendation and acceptance

Provisionally retain deterministic CPU voxel truth, derived curved near meshes and bounded far-world LOD. Neither current artifacts nor historical 400 FPS justify a whole-engine rewrite or GPU-only commitment. CPU and GPU currently accelerate different extraction work within a shared rendering architecture; both ultimately draw on GPU. Decide backend and far representation separately after the investigations below return.

The reference bridge requires preserved openings/curvature/materials; the forest requires continuous varied canopy and layered distant silhouettes. Images do not establish metres. The device matrix remains binding: 10 km PC/console, 6 km Mobile-HE; 60 FPS; voxel rendering ≤6/7/9 ms. No distance, memory or simulation-budget relaxation is authorized.

## Observations and hypotheses

Audit base: master `513ae04ca`; local `feature/showcase-draw-distance` at `eb13c3e3b` differs and forces GPU off. Preserve that branch; do not promote its product changes. No Unity/player experiment was run by this planning pass.

H1: viable mesh/LOD architecture is obscured by local publication, sampling and proxy defects. H2: even correct output cannot meet dynamic-workload/resource budgets, requiring a representation or backend change. Next discriminator: exact-player workload/identity evidence plus independent seam, publication, resource and far-path audits.

Code inspection finds actual default source-step rings of 96/192/288/409.6 m, against 400 m PC full-detail documentation; lossy far shapes/materials; reference-based cache invalidation; and validation substitutes. These are risks and contract drift, not measured frame-budget failure. Existing GPU restoration owns its repair; R04 audits evidence without competing with it. R10 reviews the already-closed far-world issue.

## Dispatch and decision gates

The ten linked issues are independent, each 30–60 minutes active work, with disjoint issue-only output. They may run concurrently; shared Unity/CI resources remain serialized by repository policy. No dependent implementation tickets are created yet. Missing evidence/fixtures remain explicit blockers.

After results: retain a backend only with correct output and dynamic workload/resource evidence; consider a bounded competing representation experiment only where a measured limit survives localized correctness repair. Preserve the authoritative core in either case. Distant visible edits/openings require an adequate derived representation; a pristine height surface alone is insufficient. Platform evidence stays platform-specific.

Review new closures on master every 30 minutes when an execution/scheduling facility is available. Check exact evidence and the merged diff; create a new small SceneIssue for each unmet criterion rather than accepting the closure. The review procedure and scheduling limitation are recorded in protocol.md. No automatic architecture decision or repair expansion is authorized by a checkbox alone.

## Remaining gates

Publish this documentation-only queue through protected-master PR; verify master. Receive and review investigation results. Only then create independent, evidence-grounded repair or alternative-prototype issues. All first-wave results are pending.
