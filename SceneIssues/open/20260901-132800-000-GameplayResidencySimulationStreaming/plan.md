# Gameplay residency / simulation streaming — implementation plan

**Target ownership:** one semantic coordination boundary at `Assets/Game/Residency/Api` / `Runtime`; Characters, CharacterAI, WorldObjects, Encounters, Persistence, WorldBuilder, GameplayReplication and VoxelEngine Streaming retain authoritative state/lifetime ownership.

## Observed behavior / acceptance

The original baseline (`ed5c6f908361228819b3368bcd8427d4b44d89e3`) already supplied stable Character/WorldObject/Encounter identities, persistence, replication, WorldBuilder semantic refs and physical Streaming. Acceptance requires one stable gameplay identity across `Dormant` / `Coarse` / `Detailed`; independent semantic demands; Detailed waiting for physical readiness and quiescing before release; server residency independent from client interest/presentation; owner-state persistence; generated-content scale; deterministic diagnostics/cost; no duplicate authority.

## Hypotheses / results

1. **Existing `IRegionStreaming` is already an ownership-safe physical-residency primitive.** Falsified: engine eviction could bypass a gameplay load-now/evict-later convention.
2. **A Streaming-owned pin plus a game-level semantic coordinator is sufficient.** Selected: Streaming owns ref-counted physical pins; Residency aggregates demands and orchestrates owner adapters only.

## Selected fix

`semantic target + independent demands` → `GameplayResidencyCoordinator` → owner adapters. Highest fidelity wins deterministically. Detailed spatial promotion acquires `IRegionResidencyLease`, waits for readiness, then realizes; demotion quiesces the owner adapter before releasing the lease. CharacterAI has a narrow coarse semantic simulation seam. WorldObject/Encounter state stays owner-owned. Proximity hysteresis is semantic/configurable and explicit control/encounter pins bypass it.

Independent proofs cover Character/AI, WorldObject, Encounter, Streaming, a 64-NPC public WorldBuilder fixture with stable IDs and bounded Detailed work, current-state GameplayReplication for a later client without server-residency ownership, and a production `SessionPersistenceService` fresh-graph round trip after residency cycling. Applicable device budgets remain 30 Hz simulation and ≤0.5 ms streaming main-thread work; no weaker feature-local limit is introduced.

## Validation / remaining gates

Original exact request `a20a3282b05d8ed0986de69e4c48b45059416936` exposed legacy `UnityEngine.Input` polling in the canonical Kentridge player under Input-System-only settings. The scoped repair uses `Keyboard.current` (`738a3b32c3a8f740ff367a91c9b4ca42a7d72ee4`) and adds the required `Unity.InputSystem` assembly reference (`66b9b0f089da00158cf475d5dc55c5c27a115817`).

Intermediate exact request `1ca35bbb8f5d4a08cb69ad44488971e4937fc4aa` (feature source `7ab20c5404e5d502dcf2f18f4d8031b4c560951b`) completed failure. Its artifact proves all 18 affected EditMode assemblies plus the focused Residency regression passed. Repository-owned player validations passed Application, Audio, Kentridge Encounter, Showcase, HUD, InventoryPresentation and ProgressionPresentation, then stopped before Residency. Root cause: the new Residency/Streaming scenarios requested 6s/5s while `player-validation.py` requires at least 10s. Raise both to 10s, then run exact-SHA validation from the resulting feature head.

Remaining gates: repository-driven module/player validation and standalone Kentridge must be green on the exact current feature SHA, with no legacy Input exception; then perform final blast-radius review, close directly to `SceneIssues/closed/...`, merge current `origin/master`, and promote only by PR + auto-merge.
