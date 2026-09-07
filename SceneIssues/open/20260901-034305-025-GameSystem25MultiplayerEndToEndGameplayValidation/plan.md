# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation; durable identity/baseline convergence; contention/conservation; combat/progression; interruption/reconnect/current-state recovery; explicit leave; configured capacity/JIP/repeated reconnect/persisted rehost; durable exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** shared validation/Kentridge composition plus required fixes in Application, Sessions, GameplayReplication, Continuity and VoxelEngine.Net. No fake networking, parallel gameplay authority, privileged mutation seam, or test-only transport.

## Proven results

Harness role isolation, exact executable/source identity, bounded semantic waits and role-attributed artifacts are exact-SHA proven. Application joined-party startup plus canonical UTP admission passed source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8` / run `34005489004`. Async formation and identity-preserving admission/retry passed source `12a33443e6fd94c30bce50d234eae2697e836a15` / run `34011275001`. Compatibility correction passed source `bfdcf1ee81ce8541669edac570c46544b998743b` / run `34028935498`.

## Current discriminator / selected work

`KentridgeMultiplayerTopologyValidation` is the real separate-process production consumer: authority enters through `ApplicationFlowCoordinator.RequestHost`, Sessions formation/provider/UTP, canonical `AuthoritativeServerSession`, and the production campaign graph; two client processes enter through `RequestJoin`. Preserved request `9edea36020facb991be0f94457b3265f31796c2e` / run `34032515299` directly parents topology source `cde588c7ea71b07eda02232a7b8570a56a5247f3` and must remain untouched while queued/running. T25-010–013 stay unchecked until built-player artifacts prove them.

While that gate waits, the branch advanced the next acceptance dependencies without moving the CI transport. Production `KentridgeMultiplayerCharacterRoster` creates deterministic `kentridge-player-N` Characters with player/combat traits and stable slot/combat bindings. `KentridgeAuthoritativeMultiplayerApplication` accepts that production roster, ensures configured capacity before admission, and constructs `PartySession` with its real `ICharacterBindingWriter`, so Sessions `party-member` binding lands in the authoritative Characters registry. The topology authority supplies the same `KentridgeCharacterHost.Characters` registry used by the production campaign graph.

The subsequent source chain through `3c890c86f99c623ee6bfb75056fe1a5f6e4604e0` adds `KentridgeMultiplayerGameplayReplication`, which composes the existing Characters/Inventory/Progression/Encounter/Vitality/Combat projection adapters. Domain queries are resolved lazily because production Sessions admission precedes campaign graph composition; unavailable owning runtimes publish an empty state under the real schema rather than a validation-owned substitute. Authority and client Application roles opt into the same descriptor set, and the topology built-player scenario now requires a post-InGame `baseline-ready` milestone carrying the replicated gameplay revision and deterministic semantic state digest. Characters plus campaign Inventory/Progression must be materially populated before that milestone can emit. These changes are intentionally unvalidated while run `34032515299` remains queued and must not trigger a replacement request.

Remaining gameplay work must keep using the existing authoritative systems and authenticated `C_PlayerInput` path. The next production change is to resolve each authenticated Net player id back through Sessions durable identity to its Characters identity, then route semantic input into real gameplay services. Contention/conservation, combat/vitality and progression scenarios must mutate only through those production commands; validation may launch/kill/relaunch processes and observe diagnostics but may not own gameplay mutation.

## Remaining execution order

1. Consume preserved topology run; record only behavior actually proven. Fix demonstrated product failure or retry only proven infrastructure after completion.
2. Validate the gameplay projection/baseline source now staged on the feature branch, then finish authenticated player-input composition and prove T25-020–023 contention, conservation, combat/vitality and progression.
3. Prove T25-030–034 process interruption/reconnect/current-state recovery and explicit leave.
4. Add release-only T25-040–043 configured capacity, JIP, repeated reconnect and persisted rehost.
5. Prove automatic selection (T25-051) and final exact-head separate-process evidence (T25-052), merge current `origin/master`, close with metadata/evidence, then PR + auto-merge and required `affected` gate.
