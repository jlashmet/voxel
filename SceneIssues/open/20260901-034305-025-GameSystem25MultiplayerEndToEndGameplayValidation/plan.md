# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build; separate authority/client processes; production formation, durable identity/baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. Every required tasks.md item remains binding.

**Ownership:** shared validation infrastructure and Kentridge composition; dependencies 06/07/08/14 plus authoritative gameplay owners. Application/Sessions/GameplayReplication/Continuity retain authority. No fake networking, socket injection, parallel gameplay authority or privileged scenario mutation. System24 is related, not a prerequisite.

## Implementation / discriminator

Latest code: `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`.

- Preserve Application correction `cb54eeec77a7178770e4a4c3260276cd9f26c520`: matching connected/gameplay-ready local membership before Prepare; strict real-Orchestration checks, single startup, failure handling and leave/rejoin. Its 26 authored tests and owned Application player proof still require execution on newer source.
- **H1 supported by inspection:** the earlier admission probe supplied a custom handler to `ServerNetworkRuntime`; canonical `AuthoritativeServerSession` instead used an inbox that did not accept admission.
- **H2 / next behavioral discriminator:** forwarding directly from transport would permit policy mutation before the authoritative tick or stale requests after disconnect. The corrected probe observes before/after the real authority tick and disconnect.
- T25-010B now queues copied admission bytes in `ServerCommandInbox`, sharing existing per-connection/global command limits. Only `ProcessAuthoritativeTick` invokes the Sessions-supplied consumer. Dead connections are removed before consumption; drained data is cleared even if policy throws. Existing authority owns reply delivery. Net still does not grant identity or readiness.
- Owning module `Assets/VoxelEngine/Net`: 11 additional inbox regression cases cover shared limits, sender attribution, copy lifetime, drain, clear and disconnect. Its existing `Validation/SessionAdmissionTransportValidation.unity` now uses canonical `AuthoritativeServerSession`, real Storage/Edits, and the same bounded probe as unit tests. Required scenario assertions include deferred consumption and discarded dead-sender requests. No new validation registration or alternate runtime.

## Exact CI / remaining gates

Original request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3` finished **success** in run `34002524305`, job `101403824713`. Downloaded artifact `9980642336` and verified SHA-256. Focused tests: 17 passed; owned assemblies: 29/5/2 passed, zero failed/skipped. All three selected player-validation targets completed. Normalized provenance/counts are in `ci-evidence-920f0e4.json`. This proves source `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`, not the later correction/Net changes or full multiplayer acceptance.

After that completion, submitted request `0078199d98f3cefe1508ae7331b23ad001b754f7`, sole parent/source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8`; only test-request.json differs. Run `34005489004`, job `101411770518`: **queued at last check**. Preserve while active. Inspect module tests/player targets before checking T25-010A/B.

Next: integrate real Sessions/provider and host/client composition without another campaign authority; complete every gameplay/recovery/Release case. Only after all gates/criteria pass: close, merge current master, PR + auto-merge, verify affected and closure on master. Last fetched master: `ef475182b866eabfe8e1d1a39c82bf7810a03f49`; local Git still fails DNS, connected GitHub works.

**Cost:** admission payload <=1,196 bytes; existing 256-per-connection/4,096-total limits shared with other commands; no packet/time/capture budgets relaxed. Smoke remains authority plus two clients; expensive cases release-tier.
