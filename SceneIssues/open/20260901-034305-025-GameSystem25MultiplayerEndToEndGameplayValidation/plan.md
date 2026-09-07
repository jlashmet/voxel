# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build with separate authority/client processes proving production formation, durable identity + exact baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity/JIP/repeated reconnect/persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** extend only production Application/Sessions/Net/Kentridge/GameplayReplication/gameplay seams plus generic validation orchestration. No fake networking, parallel gameplay authority, privileged mutation command, or test-only transport. `Game.WorldObjects.Runtime` is `noEngineReferences: true` with no meaningful scene behavior, so its durable-`CharacterId` interaction entrypoint uses module-local unit coverage; Kentridge Playable owns the separate-process integration scene.

## Material results

Harness isolation/build identity/semantic waits and Application + UTP admission are already exact-SHA proven by runs `33937957149`, `33986100313`, `33995470352`, `34005489004`, and `34011275001`.

Topology run `34032515299` attempt 2 exposed validation assembly/import compilation defects; those are fixed. Later requests `34124227235` and `34144191751` were infrastructure cancellations before product execution.

Current multiplayer composition routes authenticated Net input -> durable Party `CharacterId` -> production WorldObjects/Loot/Inventory, replicates WorldObjects state, and requires authority/client A/client B to converge on exactly one pickup transfer. Generic harness equality now requires identical `baseline-ready.revision` and `stateDigest` across all three roles.

Source `9046341431a33d67cfcea7109ac2fed8a3c76b3f` adds production Continuity admission/terminal membership policy. Exact request `c14829cb558237ab31b11550e960d14fda3e0130`, run `34150973599`, acquired `Jasons-MacBook-Pro` and failed before tests because `KentridgeContinuitySessionAdmission.cs` had two compiler errors: an untyped fallback lambda for `Func<double>` and a missing `SessionAdmissionPacket` namespace. Commit `2f978a02c8e27ec484182ce6f7df471cfebf2b55` fixes only those demonstrated errors.

## Current discriminator

Hypothesis A: the continuity compile correction allows owned tests/player validations to execute and reach topology/contention milestones. Hypothesis B: the next admitted exact run exposes a runtime composition/replication defect. **Next experiment:** exact-SHA targeted CI from the latest feature head; check T25-010–013/T25-020–021 only from green built-player artifacts.

## Remaining gates

1. Exact-head topology/contention proof; fix only demonstrated failures.
2. Reuse production Encounter/Combat/Vitality and Progression owners for T25-022/023.
3. Prove T25-030–034 reconnect/current-state recovery/explicit leave.
4. Add release T25-040–043 capacity/JIP/repeated reconnect/persisted rehost.
5. Prove T25-051/052, merge current master, close open -> closed with metadata/evidence, then PR + auto-merge and required `affected` gate.
