# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one exact packaged build; separate authority/client processes; production formation, durable identity/baseline convergence, contested gameplay/conservation, combat/progression, interruption/reconnect/current-state recovery, explicit leave, capacity/JIP/repeated reconnect/persisted rehost, and durable exact-SHA evidence. All required tasks.md items remain binding.

**Ownership:** shared validation infrastructure and Kentridge composition; dependencies 06/07/08/14 and authoritative gameplay modules. Application/Sessions/GameplayReplication/Continuity retain authority. No fake networking, socket injection, parallel gameplay authority or privileged scenario mutation. System24 is related, not a prerequisite.

## Observations / selected work

Current feature before this planning change: `d8bf34810ebd6e8d5173f52a03eb2afb2f757d64`; latest code `e3ea06d7589c0a2d13a53962a6ebccbf67489e5b`. Preserve Application correction `cb54eeec77a7178770e4a4c3260276cd9f26c520`, its real-Orchestration regressions and owned player scene.

- **H1 supported by inspection:** T25-010B currently delivers admission to a custom handler on `ServerNetworkRuntime`, but canonical `AuthoritativeServerSession` supplies `ServerCommandInbox`, which does not implement admission. The current echo probe does not exercise the shipped authority root.
- **H2 to discriminate:** merely forwarding admission from a transport callback would allow allocation/authentication before the authoritative tick and retain stale requests after disconnect. Test the actual authority root before/after ticks and disconnect, with shared per-connection/global queue limits.
- Complete existing T25-010B by adding admission to the canonical bounded inbox, draining to a Sessions-supplied consumer only from `ProcessAuthoritativeTick`, and exposing reply delivery through the existing authoritative server. Keep existing alteration/input queue cleanup and limits; no duplicate server or transport.
- Owning module: `Assets/VoxelEngine/Net`; owned tests `Tests/EditMode`, player scene `Validation/SessionAdmissionTransportValidation.unity`. Upgrade its bounded probe to the actual `AuthoritativeServerSession`, real storage/Edits and fixed-tick entry. Non-visual packet observations are not Sessions policy, gameplay acceptance or separate-process evidence.

## Exact CI / remaining gates

Preserved request `920f0e4e4883d2c8abaf77877c1f8e55c8cd4df3`, source/sole parent `8b95feaf7d849bc6a37b4d5a40a4e84b7e8c331a`, run `34002524305`, job `101403824713` is now **in progress**, running automatic module validation. Do not replace it. Inspect final results, then validate the newest feature SHA through the same transport; this older run cannot cover newer code.

Local Git fetch failed DNS resolution; connected GitHub reads/writes succeeded. Refreshed master remains `ef475182b866eabfe8e1d1a39c82bf7810a03f49`.

Next: verify latest Application/Net module tests and player scenes, integrate the real Sessions provider and party-intent/client composition without duplicating campaign authority, then complete all gameplay/recovery/Release cases. Keep required checkboxes open until proven. Only then close, merge current master, PR + auto-merge, and verify affected plus closure on master.

**Cost:** admission shares configured inbox limits with input/alteration; each payload remains at most 1,196 bytes. No existing packet, time, capture or performance budget is relaxed. Multiplayer smoke remains authority plus two clients; expensive cases remain release-tier.
