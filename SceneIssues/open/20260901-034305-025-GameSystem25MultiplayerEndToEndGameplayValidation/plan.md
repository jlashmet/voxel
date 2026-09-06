# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build, separate authority/client processes, production formation/entry, identity/baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, durable exact-SHA evidence. Every required tasks.md item is binding.

**Ownership:** shared validation infrastructure and Kentridge composition; dependencies 06/07/08/14 and authoritative gameplay modules. Application/Sessions/GameplayReplication/Continuity retain ownership. No parallel authority, fake networking, direct socket injection, or privileged scenario mutation. System24 is related, not a prerequisite.

## Current state / next discriminator

- Resumed feature `cb54eeec77a7178770e4a4c3260276cd9f26c520`. Its Application correction gates joined startup on matching connected, synchronized local membership and tests real `GameSessionOrchestrator`. It is newer than the queued request and remains unvalidated. Preserve both pre-Prepare readiness and post-initialization GameplayReady checks.
- T25-010 remains substantive: Kentridge always composes local campaign authority. Clients must not duplicate it.
- **H1 supported by inspection:** real UTP has no admission request/reply route. `ClientEventPacketReceiver` dispatches alteration/region/gameplay-repair only; `ClientNetworkRuntime` has no admission reply handler. Test sockets or interpreting an alteration packet as admission would bypass production ownership.
- **H2 / next experiment:** a narrowly typed admission envelope can reuse existing reliable EVENT delivery without changing world/input traffic. Add explicit direction-specific kinds, length validation, and bounded payloads. A server-side handler capability receives the transport-owned connection id separately and enqueues the opaque Sessions-owned admission payload; it must not authorize membership in Net. No generic unknown-packet fallback.
- T25-010B ownership: `Assets/VoxelEngine/Net`; extend existing client/server runtime surfaces, not a second transport. Owned regressions: `Tests/EditMode/SessionAdmissionTransportTests.cs`; create `Validation/SessionAdmissionTransportValidation.unity` and paired scenario. Exercise real production Net runtimes and deterministic alteration applier. Validation supplies packet inputs, instrumentation and bounded orchestration only; no fake world/art/authority. This is non-visual transport proof, not multiplayer gameplay or visual-finish acceptance.

## CI / remaining gates

Exact request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3`, source/sole parent `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`, run `34002524305`, job `101403824713` remains **queued**. Leave the request unchanged. Later source changes need later exact-SHA validation after this request completes. Historical harness/diagnostic evidence remains in tasks.md.

Local Git fetch again failed DNS resolution; connected GitHub reads/writes work. Refreshed master is `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

Complete Application and Net module validation, then real Sessions provider/admission composition and authority plus two clients. Complete all gameplay/recovery and Release cases. Do not check off unproven acceptance. Only after all required gates: close this issue, merge current master, PR + auto-merge, verify affected gate and closure on master.

**Cost:** admission messages stay within the existing 1,200-byte EVENT ceiling (4-byte envelope/length header, at most 1,196 payload bytes); encoding/decoding adds no retained allocation. No budgets or pipeline ordering change. Smoke remains authority plus two clients; expensive cases stay release-tier.
