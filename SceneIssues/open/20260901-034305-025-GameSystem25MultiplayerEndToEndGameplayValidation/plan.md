# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build with separate authority/client processes proving production formation, durable identity + exact baseline convergence, contention/conservation, combat/vitality, progression, interruption/reconnect/current-state recovery, explicit leave, configured capacity/JIP/repeated reconnect/persisted rehost, automatic selection, and final exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** extend only production Application/Sessions/Net/Kentridge/GameplayReplication/gameplay seams plus generic validation orchestration. No fake networking, parallel gameplay authority, privileged mutation command, or test-only transport. `Game.WorldObjects.Runtime` is `noEngineReferences: true`, so its durable-`CharacterId` interaction entrypoint uses module-local unit coverage; Kentridge Playable owns the separate-process runtime proof.

## Material results

Harness isolation/build identity/semantic waits and Application + UTP admission are exact-SHA proven by runs `33937957149`, `33986100313`, `33995470352`, `34005489004`, and `34011275001`. Runs `34124227235` and `34144191751` were pre-execution infrastructure cancellations.

The smoke now routes authenticated Net input -> durable Party `CharacterId` -> production WorldObjects/Loot/Inventory, campaign quest observation, and Encounter/Combat/Vitality. Cross-role assertions require exact initial baseline revision/digest equality, one conserved pickup, completed shared progression, and identical combat Vitality outcome.

Continuity compilation failures were successively isolated by runs `34150973599` and `34152962562`; `f7ab238d66cf6cad3a34c9f782659fd85a94e4ac` uses a Unity-compatible monotonic `Stopwatch` clock. Exact source `31edc4c6524afca0855a97dd683e201b7f60851b`, request `949e1dd959e722c33d65a9732a688ca74dcac660`, run `34153302952` then exposed one validation compile error: missing `Game.Characters.Api` for `CharacterVector3`, repaired by `f9784d7a925dd6649cb81d6e6ff5feaafdb97066`.

Exact source `7eea9991edcd3ea9ba67fdf498bcbbc9fd3e16bf`, request `d4c5c459187f197f29fe46dfe55ea257e1853269`, run `34154154340` passed all 86 generic harness/tool tests but failed Unity compilation before the multiplayer scene executed. The compiler proved four concrete drift classes: missing direct validation dependencies, the party presentation snapshot changing to a readonly value type, campaign town authoring changing to opaque `AuthoredTownPlan`, and an input-sink conditional requiring an explicit interface type. Source commits `e4dadd504f166d65fdd9fddeca24da638ae079f8`, `31ad127ed34713b384d60f3e78f7e08c350ee745`, `1856929c7c180d63c5f418eeb449225f224eb609`, and `f4fcdbabed2b06880aeb6951630e9b395b76cc4b` repair only those demonstrated compile/product-boundary failures: realization is derived from the authored generation plan, direct dependencies are declared, local-party lookup is struct-safe, the public WorldBuilder town facade is used, Unity `Application` is disambiguated, and the authoritative input sink is explicitly typed.

T25-030–034 are implemented but unaccepted: the harness kills client A after contention/progression; authority must observe production `ConnectionInterrupted` before a real combat input creates absent-period Vitality state; a fresh client-A process rejoins with the same member/slot/character, receives current Inventory/Progression/Vitality projections, then calls `Application.RequestLeaveGame`; authority must observe membership removal plus terminal Continuity `Left`. GameplayReplication snapshots are current-state projection replacement only and expose no historical one-shot event stream.

## Current discriminator

Hypothesis A: the repaired exact head compiles and reaches all topology/gameplay/reconnect/leave milestones. Hypothesis B: the first runtime execution exposes a production composition or ordering defect. **Next experiment:** exact-SHA targeted CI from the latest feature head; check T25-010–034 only from green built-player artifacts.

## Remaining gates

1. Exact-head smoke proof; fix only demonstrated failures.
2. Add release T25-040–043 capacity/JIP/repeated reconnect/persisted rehost.
3. Prove T25-051/052, merge current master, close open -> closed with metadata/evidence, then PR + auto-merge and required `affected` gate.
