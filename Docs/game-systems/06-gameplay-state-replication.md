# 06. Gameplay-state replication

## Decision

Build gameplay-state replication on top of the repository's existing custom `VoxelEngine.Net` client/server, protocol, interest-management, authoritative-event, and prediction/reconciliation infrastructure. Do not introduce a second multiplayer framework.

The server owns authoritative gameplay state. Networking adapts that state to and from transport/protocol messages; gameplay systems must remain usable without networking attached.

## Core boundary

Authoritative gameplay systems include characters, vitality, AI, encounters, combat, inventory, quests, and campaign/session progression.

The replication shape is:

`Authoritative gameplay -> semantic snapshot/delta adapters -> existing VoxelEngine.Net -> client replicas/presentation`

and in the opposite direction:

`Client intent -> existing VoxelEngine.Net -> authoritative command adapter -> gameplay validation/execution -> replicated authoritative result`

Clients send intent rather than claimed authoritative results.

## Character replication

The gameplay character runtime from system 03 is the primary replicated actor concept. Avoid parallel `NetworkEnemy`, `NetworkNpc`, and `NetworkPlayer` models.

A relevant character replica may include only the state a client needs to present and interact with the character, for example:

- `CharacterId`
- presentation/archetype identity
- authoritative position/facing
- vitality and defeated state
- relevant current activity
- encounter membership
- intentionally telegraphed intent when gameplay exposes it

Do not replicate server-only AI internals such as perception snapshots, planners, pathfinding internals, or private decision state.

The same replicated character identity must survive role/context transitions such as:

`town life -> encounter membership -> combat -> autonomous life`

without replacing the network entity.

## Vitality and defeat

System 02 remains authoritative for health, damage, and defeat.

Clients request gameplay actions. The server validates those actions, applies damage through actor vitality, and replicates resulting authoritative state/events such as vitality changes and defeat.

Clients must never authoritatively claim a target's resulting HP or death state.

## Encounter and combat replication

Relevant clients need enough encounter information to present current authoritative context:

- encounter instance identity and semantic `EncounterRef`
- active/completed lifecycle state
- relevant membership and encounter-local roles/teams
- final outcome when resolved

Combat networking follows command plus authoritative-result semantics. A client may request an attack/use action; the server validates legality, executes through combat/vitality systems, and replicates the resulting state.

## Explicit replication contracts

Do not serialize every internal domain event onto the network automatically. Each subsystem should expose explicit network-facing snapshot/delta contracts only for state clients actually need.

Examples may include:

- character snapshots/deltas
- vitality changes
- encounter snapshots/deltas
- combat action results
- inventory snapshots/deltas
- quest/campaign progression deltas

Internal domain events remain internal unless a client-facing consequence requires replication.

## Snapshot plus delta model

Persistent world/gameplay state needs both:

- snapshots: what is true now
- deltas/events: what changed after the snapshot

A client entering a town should receive the current relevant state of town characters rather than replaying their entire autonomous-life history.

The same requirement applies to late join and reconnect: reconstruct current relevant authoritative gameplay state, then continue with live deltas.

## Interest management

Reuse the existing networking interest-management subsystem. Gameplay replication should provide semantic relevance/ownership information that can be combined with spatial interest.

Nearby clients may receive detailed character movement/activity and active encounter state. Distant clients should not receive high-frequency detail for unrelated characters or AI.

This aligns with system 04 simulation LOD: server simulation and client replication fidelity may both decrease with distance/relevance, while authoritative state remains coherent.

## AI authority

Autonomous-life and tactical AI run authoritatively on the server. Clients receive enough resulting character state to present behavior but do not independently decide NPC goals, targets, encounter outcomes, or quest state.

## Prediction

Use the existing prediction/reconciliation infrastructure only where responsiveness requires it, especially local-player movement and possibly carefully selected immediate actions.

Do not broadly predict server-owned decisions such as:

- NPC life decisions
- enemy targeting
- encounter outcomes
- defeat
- inventory authority
- quest/campaign completion
- loot generation

## Delivery semantics

Replication contracts should distinguish state that must eventually converge from transient/interpolated state.

Examples of authoritative durable/convergent state include character existence/removal, vitality, defeat, inventory, encounter completion, and quest progression.

Examples of transient presentation-oriented state include intermediate movement/facing samples and other state for which dropping an intermediate update is acceptable.

The existing transport/protocol layer should map those semantics to appropriate channels rather than gameplay systems selecting sockets or packet behavior directly.

## Late join and reconnect

System 06 owns the state-reconstruction mechanics required by later session UX work. A joining/reconnecting client must be able to obtain relevant authoritative snapshots for characters, vitality, active encounters/combat, player inventory, quest/campaign state, and other retained session state before consuming live deltas.

System 08 may own reconnect policy and UX, but it should reuse this reconstruction path rather than inventing a second recovery model.

## Independence requirement

Character, AI, encounter, combat, and vitality simulations must be testable and runnable without a network connection. Gameplay assemblies must not directly call packet-send APIs as part of their domain behavior.

## Reuse / acceptance proof

At minimum prove both:

1. Two-client combat: a client intent is server-validated, authoritative vitality changes once, both clients converge, and encounter completion is identical.
2. Town-interest/late-observer case: an autonomous town character continues on the server; a nearby client sees detailed relevant state, a distant client does not receive unnecessary detail, and a later-arriving client receives the character's current snapshot rather than historical replay.

## Explicitly out of scope

- replacing the existing custom transport/network stack
- lobby/matchmaking/party flow (system 07)
- reconnect user experience and policy (system 08)
- gameplay rules themselves
- client-authoritative AI
- UI
- save-file persistence implementation
