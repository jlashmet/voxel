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

Exact request `a20a3282b05d8ed0986de69e4c48b45059416936` exposed legacy `UnityEngine.Input`. The reader is migrated to `Keyboard.current` at `738a3b32c3a8f740ff367a91c9b4ca42a7d72ee4`.

Request `1ca35bbb8f5d4a08cb69ad44488971e4937fc4aa` proved all 18 affected EditMode assemblies plus the focused Residency regression green, then module-player validation stopped before Residency because new Residency/Streaming scenarios requested 6s/5s while the shared harness requires ≥10s. Both scenarios are now 10s.

Request `219c6c85e71c05a97a0dda6724811c53b0897e1c` from feature SHA `15d1c74297aff86a18cd372743e1baf6bd1c5d76` completed failure during script compilation: `KentridgeWellQuestInventoryPresentation.cs` belongs to nested `Game.Kentridge.PlayableSlice`, so the earlier `Unity.InputSystem` reference on parent `Game.Composition.Kentridge.Playable` did not reach it. The dependency now lives on the owning nested asmdef and the unused parent reference is removed.

Final blast review also found an R32 teardown defect: coordinator disposal released physical leases without first quiescing Detailed adapters. The scoped fix drives targets to Dormant through normal demotion, refuses disposal while Detailed demotion is pending/failed, and adds ordering/pending regressions.

Request `634b62103b7e02452ad8383f9e0f3538b5522563` from feature SHA `4a5cf549d3a3fd00a1a51879ca8d467ce717bcd3` confirmed the Input-System assembly compile failure was resolved, then failed on a naming collision inside the newly added teardown regression fixture (`RecordingPins.Lease` property versus nested `Lease` type). Commit `9c278061d58b2797c605cf7eeefafcfab78ed012` renames the fixture members only; production behavior is unchanged.

Remaining gates: run exact-SHA validation again from the current teardown/input/scenario/test-fixture-fixed head; require repository module/player validation and standalone Kentridge green with no legacy Input exception; finish blast review, close directly to `SceneIssues/closed/...`, merge current `origin/master`, then PR + auto-merge.
