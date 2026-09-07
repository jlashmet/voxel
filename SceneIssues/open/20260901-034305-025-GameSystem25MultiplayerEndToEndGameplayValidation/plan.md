# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build with separate authority/client processes proving production formation, durable identity + exact baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity/JIP/repeated reconnect/persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** extend only production Application/Sessions/Net/Kentridge/GameplayReplication/gameplay seams plus generic validation orchestration. No fake networking, parallel gameplay authority, privileged mutation command, or test-only transport. `Game.WorldObjects.Runtime` is `noEngineReferences: true` with no meaningful scene behavior, so its durable-`CharacterId` interaction entrypoint uses module-local unit coverage; Kentridge Playable owns the separate-process integration scene.

## Material results

Harness isolation/build identity/semantic waits and Application + UTP admission are already exact-SHA proven by runs `33937957149`, `33986100313`, `33995470352`, `34005489004`, and `34011275001`.

Topology request `9edea36020facb991be0f94457b3265f31796c2e` / run `34032515299` attempt 2 acquired `Jasons-MacBook-Pro` and failed automatic module validation. Artifact `single-test-34032515299` (`sha256:bf4460c47854ac7acbc366a988006cdac1680440303cda165ae53d21e44d2218`) identified missing validation assembly/import resolution. Current source through `0bf47fa7a5e51b18d63ff3769021254db2b4fa35` fixes every demonstrated compile error.

Request `e0863932506d9b6ba71207e114f3a7af838212d8` / run `34124227235` was cancelled by concurrency before product work. Request `d943a8394bdf771c8c2bacf33de76dd091a6a83c` / run `34144191751`, directly parented by corrected feature `0bf47fa7...`, also completed cancelled with zero steps, runner id `0`, and no runner name: infrastructure non-admission, not product evidence.

Current implementation anchor `e429e4361c4ba2fb2e0a3c16691027e5a58aa6ac` composes authenticated Net input -> durable Party `CharacterId` -> production WorldObjects -> Loot -> authoritative Inventory, replicates WorldObjects semantic state, and requires both clients plus authority to converge on exactly one pickup transfer. The generic multi-process harness now compares already-consumed milestone fields across harness-attributed roles; GameSystem25 requires identical `baseline-ready.revision` and `stateDigest` for authority/client A/client B.

## Current discriminator

Hypothesis A: after the demonstrated compile fixes, the exact built-player topology reaches baseline and contention convergence. Hypothesis B: the next admitted exact run exposes a runtime composition/replication defect rather than a compile defect. **Next experiment:** exact-SHA targeted CI from the latest feature head; check T25-010–013/T25-020–021 only from green built-player artifacts.

## Remaining gates

1. Exact-head topology/contention proof; fix only demonstrated failures.
2. Reuse production Encounter/Combat/Vitality and Progression owners for T25-022/023.
3. Prove T25-030–034 reconnect/current-state recovery/explicit leave.
4. Add release T25-040–043 capacity/JIP/repeated reconnect/persisted rehost.
5. Prove T25-051/052, merge current master, close open -> closed with metadata/evidence, then PR + auto-merge and required `affected` gate.
