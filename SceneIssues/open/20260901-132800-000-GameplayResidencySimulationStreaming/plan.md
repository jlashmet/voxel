# Gameplay residency / simulation streaming — implementation plan

**Target ownership:** one semantic coordination boundary at `Assets/Game/Residency/Api` / `Runtime`; Characters, CharacterAI, WorldObjects, Encounters, Persistence, WorldBuilder, GameplayReplication and VoxelEngine Streaming retain authoritative state/lifetime ownership.

## Observed behavior / acceptance

Acceptance requires one stable gameplay identity across `Dormant` / `Coarse` / `Detailed`; independent semantic demands; Detailed waiting for physical readiness and quiescing before release; server residency independent from client interest/presentation; owner-state persistence; generated-content scale; deterministic diagnostics/cost; no duplicate authority.

## Hypotheses / results

1. **Existing `IRegionStreaming` is already ownership-safe.** Falsified: engine eviction could bypass a gameplay load-now/evict-later convention.
2. **A Streaming-owned pin plus a game-level semantic coordinator is sufficient.** Selected: Streaming owns ref-counted physical pins; Residency aggregates demands and orchestrates owner adapters only.

## Selected fix

`semantic target + independent demands` → `GameplayResidencyCoordinator` → owner adapters. Highest fidelity wins deterministically. Detailed spatial promotion acquires `IRegionResidencyLease`, waits for readiness, then realizes; demotion quiesces the owner adapter before releasing the lease. CharacterAI has a narrow coarse semantic simulation seam. WorldObject/Encounter state stays owner-owned. Proximity hysteresis is semantic/configurable and explicit control/encounter pins bypass it.

Independent proofs cover Character/AI, WorldObject, Encounter, Streaming, a 64-NPC WorldBuilder fixture with stable IDs and bounded Detailed work, late-client current-state GameplayReplication, and a production `SessionPersistenceService` fresh-graph round trip. Device authority remains 30 Hz simulation and ≤0.5 ms streaming main-thread work; no weaker feature-local budget is introduced.

## Validation / remaining gates

Exact request `a20a3282b05d8ed0986de69e4c48b45059416936` exposed legacy `UnityEngine.Input`; scoped composition repairs are `738a3b32c3a8f740ff367a91c9b4ca42a7d72ee4` (`Keyboard.current`) and `66b9b0f089da00158cf475d5dc55c5c27a115817` (`Unity.InputSystem` assembly reference).

Request `1ca35bbb8f5d4a08cb69ad44488971e4937fc4aa` proved all 18 affected EditMode assemblies plus the focused Residency regression green, then module-player validation stopped before Residency because new Residency/Streaming scenarios requested 6s/5s while the shared harness requires ≥10s. Both scenarios are now 10s.

Request `219c6c85e71c05a97a0dda6724811c53b0897e1c` was submitted from exact feature SHA `15d1c74297aff86a18cd372743e1baf6bd1c5d76` and must remain untouched while queued/running. Final blast review then found a separate R32 teardown defect: coordinator disposal released physical leases without first quiescing Detailed adapters. The scoped fix drives targets to Dormant through normal demotion, refuses disposal while Detailed demotion is pending/failed, and adds ordering/pending regressions. Therefore `219c6c85...` is diagnostic only; after it completes, exact-SHA validation must run again from the teardown-fixed head.

Remaining gates: final exact head must pass repository-driven module/player validation and standalone Kentridge with no legacy Input exception; finish blast review, close directly to `SceneIssues/closed/...`, merge current `origin/master`, then PR + auto-merge.
