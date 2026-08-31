# 16. Authoritative session persistence & restore

**Status:** Approved

## Purpose

Persist the authoritative semantic facts required to reconstruct a game session later, without serializing the live Unity/runtime object graph or duplicating persistence already owned by lower-level systems.

The defining rule is:

> Save the authoritative facts needed to recreate the game, not the runtime objects that currently happen to represent those facts.

System 16 is distinct from system 06 replication snapshots and system 08 reconnect recovery:

- **06:** authoritative server state → relevant client snapshot/deltas
- **08:** recover a player into the same running authoritative session
- **16:** authoritative session → durable state → process/session may end → fresh authoritative runtime → restore

## 1. One logically coherent session snapshot

System 16 coordinates a root durable session representation, conceptually a `GameSessionSnapshot`, containing root metadata plus versioned subsystem state.

Potential root metadata includes:

- stable session identity
- snapshot/schema version
- campaign/content identity
- world-generation/persistence identity
- authoritative simulation revision/tick
- party/session bindings
- references to versioned subsystem payloads

The exact payload may be physically split across files/stores. It must nevertheless represent one logical authoritative revision.

## 2. Owning systems define their durable state

System 16 does not inspect private fields throughout the runtime. Each authoritative subsystem with durable state exposes a narrow semantic capture/restore contract.

Examples include:

- party/member/slot bindings
- gameplay characters and vitality
- inventories
- unified quest/objective progression
- campaign/story state
- active encounter/combat state when the owning systems support complete persistence
- immutable system-15 `GameOutcome` when resolved

Existing deterministic inventory and progression snapshot models remain the foundation rather than being replaced by a second persistence representation where their semantics already fit.

## 3. Do not persist runtime machinery

Durable state must not contain implementation-lifetime objects such as:

- Unity `GameObject`, `Component`, collider, transform, or scene-instance references
- callbacks/delegates
- transport connections/socket identities
- presentation objects
- replication/interpolation queues
- pathfinding work buffers
- AI planner scratch state
- locks, tasks, temporary command buffers, or other transient runtime machinery

Those are recreated by the normal runtime composition path.

## 4. Stable identities bridge process lifetime

Persistence preserves semantic identities defined by owning systems, including where applicable:

- `PartyMemberId`
- player-slot identity
- `CharacterId`
- `InventoryId`
- `WorldObjectId`
- quest/objective refs
- campaign/story refs
- other stable authoritative IDs

After restore, a new C#/Unity object may represent an entity, but it is the same logical entity because its stable identity and semantic state are restored.

## 5. Reuse existing world/WorldObject persistence

The repository's WorldObject architecture already owns stable identity and sparse retained-state persistence. System 16 must coordinate with that existing authority rather than flattening every persistent WorldObject into a second gameplay save format.

The session snapshot may therefore retain a world-persistence revision/reference while the world subsystem owns the actual sparse world-state representation.

Apply the same principle to other lower-level systems that already own an optimized durable representation: coordinate their revision/state boundary rather than duplicate their storage semantics.

## 6. Capture one authoritative revision

A valid save cannot combine subsystem state from different logical moments.

For example, it must not capture:

- inventory after an artifact transfer;
- quest progression before that transfer's completion observation;
- world state before the artifact was removed from its container.

System 16 therefore captures at an authoritative simulation/persistence barrier:

`finish authoritative mutations for revision N`

→ `capture immutable subsystem state for revision N`

→ `release ordinary simulation`

→ serialize/store the captured state

The barrier need not block gameplay for the full disk/network write; it establishes the coherent immutable source state that is later persisted.

## 7. Atomic save publication

Never destroy the last known-good save while a new save is only partially written.

Conceptually:

1. Capture coherent revision N.
2. Write new/versioned payloads to temporary or uncommitted storage.
3. Validate required payloads/checksums.
4. Atomically publish/commit the root manifest for revision N.
5. Only then advertise revision N as the newest valid save.

An interrupted write leaves the previously committed revision loadable.

## 8. Persistence mechanism is separate from save policy

System 16 owns mechanisms such as:

- capture
- validate
- serialize/store
- load
- migrate where supported
- restore

It does not decide gameplay policy such as:

- autosave frequency
- checkpoint locations
- manual save slots
- save-on-quest-complete
- whether a menu exposes Save Now

Those policies belong to composition/UI/game design.

## 9. No permanent hard-coded "cannot save in combat" rule

System 16 should not encode arbitrary gameplay exclusions such as combat, encounters, or cutscenes being intrinsically unsaveable.

The correct rule is:

> A session is persistable only when every currently required authoritative subsystem can provide a complete durable state.

If active combat lacks a complete persistence contract in the first implementation, the coordinator may report the session temporarily not persistable. That is an implementation capability boundary, not a permanent game rule.

## 10. Restore uses the normal system-14 runtime graph

New and resumed games must use the same runtime architecture.

Restore flow is conceptually:

`load durable snapshot`

→ validate schema/content compatibility

→ system 14 creates the normal authoritative runtime graph

→ owning systems restore their semantic state

→ world persistence rebinds to the saved revision/state

→ validate authoritative consistency

→ enter gameplay-ready/running state

Do not create a separate "loaded-game runtime" implementation.

## 11. Restore establishes state; it does not replay history

Persistence represents what is true now. It does not generally reconstruct the game by replaying every historical gameplay event.

If a quest completion previously opened a gate, granted an item, and played a cutscene, restore should load the resulting quest, gate, inventory, and story state. It must not re-emit the historical completion event and accidentally duplicate rewards, cutscenes, object transitions, or terminal outcome handling.

One-shot effects need durable semantic state indicating that their consequence has already happened.

## 12. Persistence snapshots and replication snapshots may share semantics, not schemas

Reuse identical semantic DTOs where they genuinely represent the same state, but do not require persistence wire/storage formats to equal system-06 network formats.

A replication snapshot answers:

> What does this client need now?

A persistence snapshot answers:

> What state is required to reconstruct authoritative gameplay later?

Server-only durable state may never be replicated. High-frequency interpolation/presentation state may be replicated but never persisted.

## 13. Version and validate durable contracts

The root snapshot has an explicit schema version. Independently evolving subsystem payloads should also be versioned where useful.

Loading a snapshot follows one of three paths:

- directly compatible
- explicitly migrated through supported schema/content migration
- rejected as incompatible

Do not silently drop unknown required state or replace missing semantic identities with defaults.

## 14. Content compatibility remains semantic

Persist enough authored-content identity to determine whether saved semantic refs still have meaning, for example campaign/content identity plus compatibility version/hash where appropriate.

A content change need not automatically invalidate old saves. It must provide enough information for validation/migration to make that decision explicitly.

## 15. Multiplayer persistence is authoritative-server owned

Only the authoritative host/server writes session state. Clients do not upload competing versions of inventory, progression, character, or world truth for later merging.

A multiplayer re-host flow is conceptually:

`server A persists session`

→ server A ends

→ server B loads the durable session

→ same party/member/slot/character identities are reconstructed

→ players authenticate/connect with new transport connections

→ systems 06/08 synchronize them to the restored authoritative state

Transport identity is temporary; gameplay identity persists.

## 16. Resolved runs may be persisted

System 15's immutable `GameOutcome` participates in persistence when present.

Loading a resolved snapshot preserves the fact that the run is resolved and its semantic reason. Restore must not emit the terminal resolution again or silently convert the session back into a mutable running game.

Whether resolved runs may be inspected, replayed, archived, restarted, or deleted is later policy.

## 17. Restore failure is atomic from gameplay's perspective

The runtime must not become gameplay-ready with only part of a session restored.

Malformed required state, unknown critical semantic refs, failed subsystem restoration, or world/session revision mismatch aborts the restore and yields an explicit failure state.

System 14 may expose a restore lifecycle such as:

`Restoring → Ready/Running`

or

`Restoring → Failed`

but never `Restoring → partially running`.

## Reuse / integration proof

### Full gameplay round trip

1. Start a session.
2. Mutate multiple independent domains: character/vitality, inventory, progression, world objects, and campaign/story state.
3. Persist.
4. Destroy the entire runtime/process state.
5. Build a fresh runtime graph and restore.
6. Verify stable identities and semantic state match the saved revision.
7. Verify one-shot effects are not emitted twice.

### Cross-domain consistency

1. Perform an authoritative action that changes world, inventory, and progression state.
2. Persist immediately after the authoritative revision completes.
3. Restore.
4. Verify all domains reflect the same logical revision and impossible combinations are absent.

### Multiplayer re-host

1. Persist a multiplayer session.
2. Terminate the authoritative server completely.
3. Start a new server from the save.
4. Reconnect players.
5. Verify the same party/member/slot/character identities and state are restored despite new transport connections.

### Interrupted save

1. Create valid committed save A.
2. Begin writing save B.
3. Simulate interruption/corruption before publication.
4. Verify A remains loadable and B is never advertised as valid.

### Completed run

1. Resolve system 15.
2. Persist the session.
3. Reload it for supported inspection/restore behavior.
4. Verify the exact immutable outcome/reason survives and no terminal consequence is replayed.

## Out of scope

- save-slot/menu UI
- cloud/Steam/console save services
- checkpoint/retry game design
- permadeath policy
- account/profile meta-progression
- achievements
- replay/event-sourcing infrastructure
- reconnect implementation
- custom "loaded game" runtime graph
- presentation such as thumbnails or save-game labels

## Architectural constraints

- Persist semantic authoritative state rather than runtime object graphs.
- Preserve stable identities across process/session lifetime.
- Owning systems define capture/restore semantics for their own durable state.
- Reuse existing lower-level persistence authorities such as WorldObject sparse retained state.
- All subsystem state in one published save corresponds to one logical authoritative revision.
- Save publication is atomic and never destroys the last known-good revision prematurely.
- Restore uses the normal system-14 runtime graph and becomes gameplay-ready only after complete validation/restoration.
- Restore establishes current truth rather than replaying historical one-shot events.
- Server owns persistence authority in multiplayer.
