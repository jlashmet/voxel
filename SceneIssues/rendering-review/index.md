# Rendering investigation queue

Read [decision plan](plan.md) and [shared protocol](protocol.md). All issues below are first-wave, independent investigations, not implementation assignments.

| Issue | Scope | Active time |
|---|---|---|
| [R01](../open/20260905-163000-001-RendererWorkloadBaseline/plan.md) | Renderer identity and moving-frame baseline | 45–60 minutes |
| [R02](../open/20260905-163000-002-CpuCurvatureSeamDiscriminator/plan.md) | CPU curvature versus LOD seam discriminator | 45–60 minutes |
| [R03](../open/20260905-163000-003-NearEditPublicationDiscriminator/plan.md) | Near-field edit publication and coverage | 45–60 minutes |
| [R04](../open/20260905-163000-004-GpuRestorationEvidenceAudit/plan.md) | GPU restoration evidence audit | 30–45 minutes |
| [R05](../open/20260905-163000-005-RenderingResourceBudgetAudit/plan.md) | Rendering resource and distance contract audit | 45–60 minutes |
| [R06](../open/20260905-163000-006-FarCacheLifetimeDiscriminator/plan.md) | Far-feature cache lifetime and stationary cost | 30–45 minutes |
| [R07](../open/20260905-163000-007-FarSilhouetteMaterialDiscriminator/plan.md) | Far proxy silhouette, openings and materials | 45–60 minutes |
| [R08](../open/20260905-163000-008-FarDestructionPersistenceDiscriminator/plan.md) | Far destruction and handoff persistence | 45–60 minutes |
| [R09](../open/20260905-163000-009-ForestCoverageAggregationAudit/plan.md) | Forest coverage and production aggregation | 30–45 minutes |
| [R10](../open/20260905-163000-010-FarValidationEvidenceAudit/plan.md) | Far-world validation fidelity and closure review | 30–45 minutes |

Paths move from `open/` to `closed/` on completion; locate by stable ID after closure. Each directory contains `issue.json`, `plan.md`, and `tasks.md`.

References: [bridge](references/bridge.webp), [forest](references/forest.webp). These are user-supplied visual goals, not captures of this engine or measured performance evidence.

Algorithm context: [Transvoxel’s primary description](https://transvoxel.org/) supports local retriangulation and transitions between 2:1 voxel resolutions; it does not establish correctness or performance of this implementation. No renderer replacement follows from that reference alone.
