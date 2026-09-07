# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Acceptance:** one packaged build; separate authority/client processes; production formation; durable identity/baseline convergence; contention/conservation; combat/progression; interruption/reconnect/current-state recovery; explicit leave; configured capacity/JIP/repeated reconnect/persisted rehost; durable exact-SHA evidence. Every `tasks.md` criterion remains binding.

**Ownership:** shared validation/Kentridge composition plus required fixes in Application, Sessions, GameplayReplication, Continuity and VoxelEngine.Net. No fake networking, parallel gameplay authority, privileged mutation seam, or test-only transport.

## Proven results

Harness role isolation, launch/kill/relaunch, exact executable/source identity, monotonic semantic waits, and role-attributed artifacts are already exact-SHA proven. Application joined-party startup plus canonical UTP admission passed exact source `f678a5f7e44a6d6e9366316d006d25bd7ebe27b8` in run `34005489004`. Cancellable async formation and identity-preserving admission/retry passed exact source `12a33443e6fd94c30bce50d234eae2697e836a15` in run `34011275001`.

Request `fc4e467e202c295459d775ee64c450afab6f15e8` failed run `34025141968` on three test-only `byte[].AsSpan(...)` compiler errors. The scoped compatibility fix was validated by request `046cca7d61f7052828878fd749050dbf592f6f88`, run `34028935498`, which completed successfully for feature source `bfdcf1ee81ce8541669edac570c46544b998743b`.

## Current discriminator and selected fix

The generic harness is sufficient; the remaining topology gap is a real separate-process consumer of the production Kentridge multiplayer wrappers. The branch now stages `KentridgeMultiplayerTopologyValidation`: authority uses `KentridgeSessionRuntimeGraphFactory`, the production campaign bootstrap, `ApplicationFlowCoordinator.RequestHost`, Sessions formation, canonical `AuthoritativeServerSession`, and UTP. Client A/B use `RequestJoin`, the production formation service, gameplay replication, and `KentridgeReplicatedClientSessionGraphFactory`. The scenario launches authority first, then A then B, so slot/member/character allocation is deterministic and all three roles must report the same three-member topology signature.

This implementation is **not yet acceptance evidence**. T25-010 through T25-013 remain unchecked until exact-SHA CI compiles the new validation assembly and the built multi-process player target passes. Current request `9edea36020facb991be0f94457b3265f31796c2e`, run `34032515299`, directly parents topology feature source `cde588c7ea71b07eda02232a7b8570a56a5247f3`; it must remain untouched while queued/running.

## Production gameplay convergence path discovered while topology gate waits

Do not add a second replication protocol or validation-owned mutation surface. `Game.GameplayReplication.Adapters` already contains the semantic projection sources needed by T25-013/T25-021/T25-022/T25-023: Characters, Inventory, Vitality, Encounters, Combat and Progression. `GameplayPublicationBuilder` fixes its source set at construction, while `GameplayReplicationReadState` fixes its known descriptors at construction, so the Kentridge multiplayer composition must register the production gameplay source/descriptor set when authority/client roles are created rather than attempting late test-only injection.

The authoritative Kentridge campaign graph already owns the required gameplay state. `KentridgeCampaignSession` exposes Inventory and the campaign runtime/Progression; `KentridgeCharacterHost` owns Characters; the existing `KentridgeForestBanditEncounter` production extension owns Encounter, Combat and Vitality queries after graph composition. Preserve the graph's no-service-locator boundary by composing a narrow Kentridge Playable projection set that closes over those production owners and exposes only `IGameplayProjectionSource`/`GameplayProjectionDescriptor` to the multiplayer wrappers. Sources whose concrete query is not live until graph composition should keep a stable descriptor and capture an empty semantic state until the production graph is live; they must not manufacture gameplay state. Keep these extended projections out of the formation `GameplayReady` prerequisite until the session graph exists, then make T25-013 explicitly wait for matching current semantic revisions/states before mutations.

All gameplay commands that originate from a remote client must stay on the existing authenticated network path. `ClientNetworkRuntime.TrySendPlayerInput` sends `C_PlayerInput` with no claimed player id; `AuthoritativeServerSession` resolves connection-owned identity and invokes `IAuthoritativePlayerInputSink` during the fixed authoritative tick. Replace the topology validation's current `NoInputSink` only with a production Kentridge input adapter that maps the authenticated player/ActionBits into existing public gameplay intents. Do not call Inventory/Combat/Progression authorities directly from validation code. For contention, prefer a real Loot/WorldObject claim only if Kentridge production composition owns that authority; otherwise add the missing Kentridge gameplay composition as production work rather than faking the claim in the test harness.

## Remaining execution order

1. Consume the preserved topology exact-SHA run; if green, record T25-010–013 evidence only for behavior actually proven by its artifacts. If product-failed, fix the demonstrated cause; if infrastructure-failed, retry only after the run is complete and classified.
2. Add the production Kentridge gameplay projection set and authenticated input mapping, then extend the same separate-process smoke to baseline semantic convergence and T25-020–023 contention/conservation/combat/progression.
3. Extend production continuity/session lifecycle coverage for T25-030–034 with process kill/relaunch and explicit Leave kept distinct.
4. Add release-only configured-capacity, join-in-progress, repeated reconnect and persisted rehost scenarios for T25-040–043.
5. Prove automatic discovery/selection (T25-051), obtain final exact-feature-head separate-process evidence (T25-052), reconcile current `origin/master`, close with metadata/evidence, and promote only through the normal PR + auto-merge path.

**Remaining gates:** exact-SHA topology smoke; T25-020–023 gameplay convergence; T25-030–034 reconnect/leave; T25-040–043 release scenarios; T25-051 automatic selection; T25-052 final evidence; reconcile current `origin/master`; closure metadata; PR + auto-merge.
