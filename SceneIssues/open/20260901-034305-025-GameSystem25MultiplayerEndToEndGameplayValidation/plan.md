# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build; separate authority/client processes; production formation, durable identity/baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, durable exact-SHA evidence. All required tasks.md items remain binding.

**Ownership:** shared validation infrastructure and Kentridge composition; dependencies 06/07/08/14 and authoritative gameplay modules. Application/Sessions/GameplayReplication/Continuity retain authority. No fake networking, socket injection, parallel gameplay authority or privileged scenario mutation. System24 is related, not a prerequisite.

## Implementation / next discriminator

Latest code: `e3ea06d7589c0a2d13a53962a6ebccbf67489e5b`. Planning was reconciled on `ed0c4cd70ad89fdfc13366e8f6e4e42e87694da4`; a stale fast-forward was rejected and no feature work was overwritten.

- T25-010A: preserve correction `cb54eeec77a7178770e4a4c3260276cd9f26c520`, its 26 authored cases and owning Application player scene. Active party state does not imply local synchronization; wait for connected/gameplay-ready local membership before Prepare, retain strict Orchestration binding checks and the distinct post-initialization readiness wait. Execution remains pending.
- **H1 supported by source inspection:** the existing UTP dispatcher lacked admission traffic. **H2 awaiting execution:** explicit bounded request/reply framing can reuse EVENT without disturbing world/input replication.
- T25-010B now implements `C_SessionAdmission`/`S_SessionAdmission`, exact-length/direction checks, transport-owned sender attribution and optional bounded-queue handler contracts. `ClientNetworkRuntime` uses its existing host; the server reply extension uses existing `ServerNetworkRuntime.TrySend`. Unhandled/malformed admission fails closed. Net neither interprets credentials nor grants membership/readiness; the real Sessions provider still needs integration.
- Owning module: `Assets/VoxelEngine/Net`. Added 27 authored `SessionAdmissionTransportTests` cases and `Validation/SessionAdmissionTransportValidation.unity` with paired executable scenario. The shared bounded probe uses two real Net clients, real server runtime and deterministic alteration applier: maximum-size roundtrip, reply isolation, existing alteration traffic and fresh transient connection. It is non-visual transport instrumentation, not separate-process gameplay, Sessions admission or visual-finish evidence.
- Reviewed committed diff: existing client adds 22 lines/removes one; protocol adds four registry entries/lines and server dispatch adds six lines. Remaining changes are codec/contracts and owned tests/validation. No CI request, pipeline configuration or other SceneIssue changed.

## Exact CI / remaining gates

Preserve `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3`, source/sole parent `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`, run `34002524305`, job `101403824713`: **still queued, no executed tests at last inspection**. No replacement/additional request was created. Both newer source changes need later exact-SHA validation after this run completes. Historical harness evidence remains in tasks.md.

Local Git fetch failed DNS resolution; connected GitHub reads/writes succeeded. Last refreshed master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

Next: finish queued validation; validate latest Application/Net source and owned scenes; integrate real Sessions provider and party-intent/client composition without duplicating campaign authority. Complete all gameplay/recovery/Release cases. Keep required checkboxes open until proven. Only then close, merge current master, PR + auto-merge, verify affected gate and closure on master.

**Cost:** existing 1,200-byte EVENT ceiling; 4-byte header and at most 1,196 payload bytes, no retained codec allocations. Probe buffers are fixed-capacity; tests/player deadlines are 5/6 seconds. No existing budgets changed.
