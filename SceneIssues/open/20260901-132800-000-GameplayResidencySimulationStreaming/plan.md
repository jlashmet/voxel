# Gameplay residency / simulation streaming — implementation plan

**Target ownership:** one semantic coordination boundary at `Assets/Game/Residency/Api` / `Runtime`; Characters, CharacterAI, WorldObjects, Encounters, Persistence, WorldBuilder, GameplayReplication and VoxelEngine Streaming retain authoritative state/lifetime ownership.

## Observed behavior / acceptance

The original baseline (`ed5c6f908361228819b3368bcd8427d4b44d89e3`) already supplied stable Character/WorldObject/Encounter identities, persistence, replication, WorldBuilder semantic refs and physical Streaming. Acceptance requires one stable gameplay identity across `Dormant` / `Coarse` / `Detailed`; independent semantic demands; Detailed waiting for physical readiness and quiescing before release; server residency independent from client interest/presentation; owner-state persistence; generated-content scale; deterministic diagnostics/cost; no duplicate authority.

## Hypotheses / results

1. **Existing `IRegionStreaming` is already an ownership-safe physical-residency primitive.** Falsified: engine eviction could bypass a gameplay load-now/evict-later convention.
2. **A Streaming-owned pin plus a game-level semantic coordinator is sufficient.** Selected: Streaming owns ref-counted physical pins; Residency aggregates demands and orchestrates owner adapters only.

## Selected fix

`semantic target + independent demands` → `GameplayResidencyCoordinator` → owner adapters. Highest fidelity wins deterministically. Detailed spatial promotion acquires `IRegionResidencyLease`, waits for readiness, then realizes; demotion quiesces the owner adapter before releasing the lease. CharacterAI has a narrow coarse semantic simulation seam. WorldObject/Encounter state stays owner-owned. Proximity hysteresis is semantic/configurable and explicit control/encounter pins bypass it.

Independent proofs now cover Character/AI, WorldObject, Encounter, Streaming, a 64-NPC public WorldBuilder fixture with stable IDs and bounded Detailed work, current-state GameplayReplication for a later client without server-residency ownership, and a production `SessionPersistenceService` fresh-graph round trip after residency cycling. Applicable device budgets remain 30 Hz simulation and ≤0.5 ms streaming main-thread work; no weaker feature-local limit is introduced.

## Validation / remaining gates

Original exact request `a20a3282b05d8ed0986de69e4c48b45059416936` completed with module validation green but mandatory Kentridge standalone replay failed on baseline legacy `UnityEngine.Input` polling under Input-System-only Player Settings. This demonstrated acceptance blocker is fixed narrowly at composition commit `738a3b32c3a8f740ff367a91c9b4ca42a7d72ee4` using `Keyboard.current`; no new input authority was added.

Intermediate exact request `1ca35bbb8f5d4a08cb69ad44488971e4937fc4aa` validates pre-input-fix feature SHA `7ab20c5404e5d502dcf2f18f4d8031b4c560951b` and must be left untouched while queued/running. After it completes, validate the final feature SHA including this plan/checklist and input repair; require affected-module and standalone-player gates green. Then close directly to `SceneIssues/closed/...`, merge current `origin/master` into `fixes/agent-3`, and promote only by PR + auto-merge.
