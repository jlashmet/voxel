# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build with separate authority/client processes proving production formation, durable identity + exact baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity/JIP/repeated reconnect/persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** extend only production Application/Sessions/Net/Kentridge/GameplayReplication/gameplay seams plus generic validation orchestration. No fake networking, parallel gameplay authority, privileged mutation command, or test-only transport. `Game.WorldObjects.Runtime` is `noEngineReferences: true` with no meaningful scene behavior, so its durable-`CharacterId` interaction entrypoint uses module-local unit coverage; Kentridge Playable owns the separate-process integration scene.

## Material results

Harness isolation/build identity/semantic waits and Application + UTP admission are exact-SHA proven by runs `33937957149`, `33986100313`, `33995470352`, `34005489004`, and `34011275001`. Topology run `34032515299` exposed validation assembly/import compilation defects; those are fixed. Runs `34124227235` and `34144191751` were infrastructure cancellations before product execution.

Current multiplayer composition routes authenticated Net input -> durable Party `CharacterId` -> production WorldObjects/Loot/Inventory, replicates WorldObjects state, and requires authority/client A/client B to converge on exactly one pickup transfer. The same smoke feeds the campaign's real quest observation path and production Encounter/Combat/Vitality owners; generic cross-role equality requires identical baseline revision/digest and combat-vitality result.

Source `9046341431a33d67cfcea7109ac2fed8a3c76b3f` added production Continuity admission/terminal membership policy. Run `34150973599` exposed two continuity compile errors, fixed in `2f978a02c8e27ec484182ce6f7df471cfebf2b55`. Follow-up exact request `557b5fec9eaeb3354edaa9e8b3a057da0466d691`, run `34152962562`, then isolated one Unity-profile incompatibility: `Environment.TickCount64` is unavailable. Commit `f7ab238d66cf6cad3a34c9f782659fd85a94e4ac` replaces only that clock source with monotonic `Stopwatch` timestamps.

## Current discriminator

Hypothesis A: the compatibility correction lets owned tests/player validations execute and reach topology/contention/progression/combat milestones. Hypothesis B: the next admitted exact run exposes a runtime composition/replication defect. **Next experiment:** exact-SHA targeted CI from the latest feature head; check acceptance only from green built-player artifacts.

## Remaining gates

1. Exact-head topology/contention/progression/combat proof; fix only demonstrated failures.
2. Prove T25-030–034 interruption, absent-period mutation, reconnect/current-state recovery, and explicit leave using production Continuity/Application paths.
3. Add release T25-040–043 capacity/JIP/repeated reconnect/persisted rehost.
4. Prove T25-051/052, merge current master, close open -> closed with metadata/evidence, then PR + auto-merge and required `affected` gate.
