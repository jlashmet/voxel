# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build; separate authority/client processes; production formation, durable identity/baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. All required tasks.md items remain binding.

**Ownership:** shared validation infrastructure and Kentridge composition; dependencies 06/07/08/14 and authoritative gameplay modules. Application/Sessions/GameplayReplication/Continuity retain authority. No fake networking, socket injection, parallel gameplay authority, or privileged scenario mutation. System24 is related work, not a prerequisite.

## Implementation / discriminator

Latest code: `cb54eeec77a7178770e4a4c3260276cd9f26c520`, following initial joined-start code `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`.

- **H1 supported by inspection; execution pending:** Active describes party lifecycle, not local synchronization. Real `GameSessionOrchestrator.Prepare` rejects unready bindings; the earlier fixture permitted them and masked this mismatch.
- **H2 / distinct case retained:** readiness can drop after composition/initialization, requiring the existing StartingSession wait.
- Correction waits for matching connected/gameplay-ready local membership before consuming the startup attempt. It preserves session/member matching, leader-only Start, strict graph validation, one-shot failure behavior, and leave/rejoin reset.
- The 26 authored `ApplicationJoinedPartyStartupTests` now delegate lifecycle computation to production Orchestration instead of a substitute state machine. Added pre-Prepare readiness discrimination, graph rejection despite a ready projection, and six disconnected/recovery/left states.
- Owning module: `Assets/Game/Application`. Its existing `Validation/ApplicationFrontendValidation.unity` now includes `ApplicationJoinedPartyValidation`, invoking production Application/Orchestration with bounded readiness inputs. Scenario assertions require synchronization-wait, single startup, teardown and fresh rejoin. Superseded joined-start stub proof was replaced; unrelated frontend checks remain unchanged. This non-visual boundary fixture does not prove network topology or gameplay acceptance.

Committed diff reviewed; execution remains unverified. Immediate blast radius is Application plus its tests/scene/scenario. No timing, capture or performance budgets changed.

## Exact CI / remaining gates

Preserve request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3`, source/sole parent `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`, run `34002524305`, job `101403824713`: **queued throughout this continuation; no runner/test result at last inspection**. No replacement or additional request was created. Newer code requires later exact-SHA validation after this run completes.

Local Git fetch again failed DNS resolution; connected GitHub reads/writes succeeded. Refreshed master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

T25-010/011 still need production provider/UTP admission and client composition without another campaign authority; the inspected EVENT dispatcher lacks admission/party-intent routing. Complete that topology, gameplay/recovery cases, Release scenarios and all exact-SHA gates. Keep unproven checkboxes open. Only then close this issue, merge current master, PR + auto-merge, and verify the affected gate plus closure on master.

**Cost:** one startup attempt per formation. Multiplayer smoke remains authority plus two clients; expensive cases remain release-tier.
