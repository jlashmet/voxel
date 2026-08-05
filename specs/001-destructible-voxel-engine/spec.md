# Feature Specification: Destructible & Buildable Multiplayer Voxel World

**Feature Directory**: `001-destructible-voxel-engine`
**Created**: 2026-08-04
**Status**: Draft
**Input**: User description: "Fully destructible voxel environment with many players, plus runtime building, in a world too large to fit in memory at once. Built in Unity."

## Overview

A multiplayer voxel world in which every part of the environment can be destroyed and rebuilt by players at runtime, in a world larger than client memory. The value proposition is that terrain and structures are genuine gameplay material rather than scenery: cover can be removed, routes can be carved, and fortifications can be raised, and every player sees the same world state.

The three capabilities are mutually constraining — destruction and building make the world state mutable and therefore expensive to replicate, while a large world makes it impossible to hold resident. The specification treats them as one feature because solving any two in isolation produces an architecture that cannot absorb the third.

This specification supersedes the prior proposal document (`📘 Decoupled Voxel Rendering & Multiplayer Synchronization Architecture`, CTBS + CGVAVS v1.0). That document's central mechanism — a single shared "confidence map" driving both network reconciliation and render detail — was reviewed and rejected; see `architecture-notes.md` for the review findings and the material retained from it.

## User Scenarios & Testing *(mandatory)*

### Primary User Story

A player joins a session already in progress, moves through a world that previous players have visibly altered, destroys part of a structure to create a firing line, builds cover in the resulting gap, and sees other players react to both changes in real time. The player then travels several kilometres across the world without loading screens or stalls, and the alterations they made remain when they return.

### Acceptance Scenarios

1. **Given** two players observing the same wall, **When** one destroys a section of it, **Then** the other sees the same section removed and can move through the resulting gap, with no perceptible difference between their two views of the geometry.
2. **Given** a player standing on a structure, **When** another player destroys the supports beneath it, **Then** the unsupported portion collapses rather than remaining suspended, and both players observe the same collapse outcome.
3. **Given** a player placing a structure, **When** the placement is permitted, **Then** it appears immediately for the placing player and shortly after for others; **When** the placement is not permitted, **Then** the placing player is told why and the structure does not persist.
4. **Given** a player travelling continuously across the world, **When** they cross into regions not previously visited, **Then** those regions appear without a loading screen and without a visible stall in frame rate.
5. **Given** a distant region that another player has heavily altered, **When** a player looks toward it from several kilometres away, **Then** the alteration is visible in the silhouette of the landscape rather than the region appearing unmodified.
6. **Given** a player who leaves and later returns to a region they altered, **When** they arrive, **Then** their alterations are still present.
7. **Given** a session at target player count with sustained heavy destruction, **When** measured over the session, **Then** all players continue to receive world updates without degradation of responsiveness.

### Edge Cases

- **Concurrent conflicting edits**: two players build into the same empty space in the same instant. Exactly one outcome must become authoritative and both players must converge on it.
- **Edits spanning region boundaries**: a structure straddles two regions and only one is loaded. Collapse behaviour must be consistent regardless of which regions happen to be resident.
- **Mass destruction events**: a single action alters a very large volume at once. This must not produce a network or frame-rate spike proportional to the affected volume.
- **Griefing via construction**: a player attempts to seal others in, block objectives, or exhaust world resources by unbounded placement.
- **Unbounded world growth**: cumulative player alteration causes stored world state to grow without limit over a long-running session.
- **Rapid traversal**: a player moves or teleports faster than regions can be brought in.
- **Boundary oscillation**: a player lingers exactly at a region boundary, causing repeated load and unload of the same region.
- **Late join and reconnect**: a player joins mid-session or reconnects after a dropout and must acquire current world state without replaying the full history of the session.
- **Client falsification**: a client reports alterations it is not entitled to make, or claims to have made them at a position it never occupied.
- **Player in destroyed volume**: a player occupies space that another player destroys or builds into.

## Requirements *(mandatory)*

### Functional Requirements

**World alteration**

- **FR-001**: Every voxel of the environment MUST be destructible by players, with no designated indestructible geometry other than explicitly designated protected zones (FR-014).
- **FR-002**: Players MUST be able to add material to the world at runtime, using both bounded multi-voxel brush shapes and single-voxel precision placement.
- **FR-003**: Material detached from its supporting structure MUST become falling debris and come to rest, rather than remaining suspended.
- **FR-004**: Newly placed material MUST be required to connect to existing structure, and material that loses adequate support MUST collapse.
- **FR-005**: The world MUST support at least two material classes with distinct destruction behaviour, so that players can make meaningful choices about what to build with.

**Shared world state**

- **FR-006**: All players MUST converge on identical world state for any region they can observe, with no persistent divergence between clients.
- **FR-007**: The server MUST be authoritative over world state; no client-reported alteration may take effect without server adjudication.
- **FR-008**: Alterations MUST appear to the acting player immediately, without waiting for server confirmation.
- **FR-009**: Where an immediate local prediction is later rejected by the server, the player MUST be shown that the state was provisional and MUST be given the reason for rejection.
- **FR-010**: The world state that determines whether a shot connects or a player is blocked MUST agree with the world state the player sees.
- **FR-011**: Concurrent conflicting alterations MUST resolve deterministically to a single outcome that all clients adopt. Arbitration MUST be total and reproducible — given the same set of competing alterations, every client and the server MUST select the same winner regardless of arrival order.
- **FR-032**: When a player occupies space that another player destroys or builds into, the system MUST resolve the overlap deterministically and identically for all observers, and MUST NOT leave a player intersecting solid matter.

**Scale and streaming**

- **FR-012**: The world MUST be larger than the memory available on a client, with only the portion relevant to a player resident at any time.
- **FR-013**: Regions MUST be brought in and released during play without loading screens and without a perceptible frame-rate stall.
- **FR-014**: Alterations to regions beyond full-detail range MUST remain visible at reduced fidelity, so that distant changes to the landscape are apparent.
- **FR-015**: Client memory consumption MUST be bounded by a configured budget rather than by world size, and MUST not grow over a long session.
- **FR-016**: A player travelling at maximum traversal speed MUST continue to be presented with world content, degrading to reduced fidelity rather than to absent geometry.
- **FR-017**: Alterations MUST persist for the lifetime of the session and MUST be present when a player returns to a region.

**Moderation and integrity**

- **FR-018**: Designated protected zones MUST exist in which alteration is disallowed, and the server MUST reject alterations within them.
- **FR-019**: The system MUST limit how much a single player may alter within a time window, and MUST limit how densely any area may be filled.
- **FR-020**: Player-placed material MUST record its originating player, so that alterations can be attributed for moderation.
- **FR-021**: The server MUST reject alterations that are physically implausible for the acting player, including alterations beyond their reach or in regions they cannot perceive.
- **FR-022**: Stored world state MUST be bounded over a long-running session by some combination of compaction, budgets, or expiry of player-placed material.
- **FR-023**: Operators MUST be able to inspect the history of alterations for a region in order to investigate griefing.

**Platform**

- **FR-026**: Players on PC, console, and mobile MUST be able to occupy the same world instance simultaneously and interact without restriction.
- **FR-027**: The world MUST be presentable at a reduced fidelity tier that meets the memory and compute budget of the lowest supported device class, without altering world state, collision outcomes, or the result of any player action.
- **FR-028**: The client MUST detect or be configured with its device class and select the corresponding fidelity tier, including a reduced full-detail radius and an earlier transition to reduced-fidelity distant representation.
- **FR-029**: The network protocol MUST remain within the bandwidth budget of a constrained mobile connection at the target player count, and MUST degrade fidelity of received world detail rather than dropping world state correctness when bandwidth is constrained.
- **FR-030**: The system MUST tolerate the higher packet loss and latency variance typical of mobile networks without producing divergent world state.

**Session**

- **FR-024**: Players joining mid-session or reconnecting MUST acquire current world state for their surroundings without replaying the full session history.
- **FR-025**: The system MUST support the target concurrent player count (see Assumptions) in a single shared world instance without degradation of world-update responsiveness.
- **FR-031**: World alterations MUST persist for the duration of a session and MUST be discarded at session end, so that each session begins from the unaltered generated world.

### Key Entities

- **Voxel**: The smallest unit of world material. Carries a material class. Discrete — it is present or absent, with no intermediate state.
- **Brick**: A small fixed-size cubic block of voxels, the unit of storage allocation. Bricks that are entirely empty or entirely one material cost no storage.
- **Region**: The unit of streaming, persistence, and moderation. Owns a bounded volume of the world, is loaded and released as a whole, and carries its own history of alterations.
- **Alteration Event**: A single adjudicated change to the world — a destruction or a placement — attributed to a player, positioned, and timestamped. The unit of replication and of the moderation record.
- **Speculative Overlay**: A client-local, visually distinct layer holding alterations predicted but not yet confirmed by the server.
- **Debris Body**: Material that has detached from the world and is falling, until it comes to rest and rejoins the world.
- **Detail Level**: A coarsened representation of a region's contents, used to present distant regions at reduced fidelity and cost.
- **Protected Zone**: A designated volume in which player alteration is disallowed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

All numeric budgets referenced below are defined in [device-matrix.md](./device-matrix.md) and are the authoritative targets. Where a criterion says "the budget", it means that document's value for the relevant tier.

- **SC-001**: With the target number of players in one world under sustained heavy destruction, every player sees world changes appear within the world-update latency budget, with no player's update rate degrading more than 10% relative to the session median.
- **SC-002**: A single large destruction event affecting thousands of voxels is transmitted at a cost comparable to a single ordinary player action, rather than proportional to the volume it affects.
- **SC-003**: Two clients observing the same region after 10,000 alteration events show byte-identical world state.
- **SC-004**: A player travelling continuously at maximum traversal speed for ten minutes experiences no loading screen and no frame exceeding the tier's frame budget attributable to world streaming.
- **SC-005**: Client memory attributable to the world remains within its configured budget throughout a two-hour session, with no upward trend.
- **SC-006**: A landscape alteration large enough to change a silhouette is identifiable by a player observing from the maximum supported view distance.
- **SC-007**: Fewer than 1 in 1,000 player build actions are rejected after being shown locally; of those rejected, 100% present the player with a reason.
- **SC-008**: Structures whose support is removed collapse in a way that all observing players agree on, in 100% of trials, including where the structure spans a region boundary and one side is not resident.
- **SC-009**: A player joining mid-session is playable within the same time budget as a player joining an unaltered world, regardless of how heavily the world has been altered.
- **SC-010**: Server storage for the world grows sub-linearly with cumulative player alterations over a session, and a session of the target duration completes without exhausting the storage budget.
- **SC-011**: No sequence of legal client messages allows a player to alter the world outside the limits of FR-018 through FR-021.
- **SC-012**: In playtesting, players correctly predict whether a wall will stop a shot at least 95% of the time — i.e. the visual and collision worlds are not observably different.
- **SC-013**: A player on the lowest supported device class and a player on the highest, performing the same action against the same part of the world, obtain the same outcome in 100% of trials.
- **SC-014**: A session at target player count under sustained heavy destruction stays within the mobile-tier sustained and peak bandwidth budgets for every participant.
- **SC-015**: On the lowest supported device class, the world is presented within that tier's memory budget and frame budget, across the full kilometre-scale world including regions altered by other players.
- **SC-016**: Under the packet loss and latency variance figures defined for the mobile tier, clients converge to identical world state (per SC-003) with no manual intervention.
- **SC-017**: Given a set of concurrent conflicting alterations delivered to clients in differing orders, every client and the server select the same winning alteration in 100% of trials (FR-011).
- **SC-018**: In 100% of trials where a player occupies space that is destroyed or built into, all observers agree on the resulting player position and no player is left intersecting solid matter (FR-032).

## Assumptions

Recorded defaults where the input was silent. Each is a decision that can be revisited but is treated as settled for planning.

- **Voxel scale**: 10–20 cm. Fine enough for destruction to read as material rather than as blocks, coarse enough for a large world.
- **Concurrent players**: 32–64 in one shared world instance, from the first playable. *(Resolved: Q1 = B.)*
- **World extent**: kilometre-scale, several kilometres per axis, from the first playable. Streaming is a phase-one requirement, not a later addition. *(Resolved: Q1 = B.)*
- **Base terrain**: procedurally generated from a seed, so that unaltered world costs no storage or bandwidth. Only alterations are stored and transmitted.
- **Persistence horizon**: alterations persist for the duration of a session and are discarded when the session ends. *(Resolved: Q2 = A.)*
- **Device tiering**: the same world is presented at different fidelity per device class. All device classes share identical world state and identical collision outcomes; only presentation fidelity varies. Three tiers — PC, console, high-end mobile — defined with concrete budgets in [device-matrix.md](./device-matrix.md).
- **Mobile scope**: recent flagship phones only. Mid-tier and low-tier mobile are out of scope, which raises the floor the whole system is budgeted against and materially reduces the rendering risk.
- **Fidelity of debris**: debris is a visual and tactical effect that comes to rest; full rigid-body simulation of every fragment is not required.
- **Fluids, fire, growth, erosion**: not required. Material is static once at rest.
- **Determinism**: any cross-client agreement mechanism relies on integer arithmetic on the CPU. GPU floating-point results are not assumed reproducible across vendors or drivers.
- **Anti-cheat posture**: server-side plausibility rejection and attribution logging. Client attestation and anti-tamper are out of scope.
- **Traversal speed**: on-foot and vehicle-speed movement. Instantaneous teleport across the world is permitted but may present reduced fidelity briefly.

## Constraints

- **C-001**: The implementation target is Unity. The architecture must be expressible within Unity's rendering, job, and memory model, and must not require engine source modification.
- **C-002**: The target is PC, console, and **high-end mobile only** with crossplay in a single shared world instance. Mid-tier and low-tier mobile are explicitly out of scope. The client must still be tiered from the first playable rather than ported later, and budgets are still specified against the lowest supported class and allowed to scale up — but that floor is now a recent flagship phone rather than a mass-market device. Concrete per-tier budgets are defined in [device-matrix.md](./device-matrix.md).
- **C-006**: Device class may affect presentation fidelity only. World state, collision outcomes, and the result of any player action MUST be identical across device classes; no device class may be advantaged or disadvantaged by what it can render.
- **C-003**: World state is discrete. There is no partial or blended voxel occupancy — a voxel is present or absent — and any mechanism that requires blending between the two is disallowed, because collision cannot be blended.
- **C-004**: The visual representation and the collision representation must derive from the same data. Divergence between them is a defect, not a tuning parameter.
- **C-005**: The server is authoritative. Client-side prediction is a presentation technique and never a source of truth.

## Out of Scope

- Cross-session world persistence. Sessions begin from the unaltered generated world; alterations are discarded at session end. This also removes moderation-at-rest, backup, and long-horizon decay policy from scope.
- Fluid, fire, weather, erosion, and vegetation growth simulation.
- Game-specific content: weapons, objectives, scoring, progression, economy.
- Client-side anti-tamper and attestation.
- **Mid-tier and low-tier mobile devices.** Only recent flagship phones are supported. This is a deliberate narrowing that raises the budget floor and removes the project's largest rendering risk.
- Author-time level editing tooling beyond what is needed to seed test worlds.
- Voice chat, matchmaking, lobbies, accounts.

## Dependencies

- Unity, with a scriptable render pipeline, a compute-shader-capable graphics target, and a burst-compiled job system.
- A compute-shader-capable graphics API on every target device class. On high-end mobile this means Vulkan 1.1+ or Metal 3, both of which are reliably present on the target hardware. Still verified on real hardware before the rendering path is committed, but no longer a project-threatening gate.
- A networking transport supporting unreliable, reliable, and separately-prioritised bulk channels, available on all target platforms including mobile.
- A server-side key-value store for cold region state, sized for one session rather than for indefinite retention.
- Dedicated server hosting; peer-to-peer is not viable given the authority requirement.
- Console platform certification requirements and mobile store policies, which constrain networking, storage, and update mechanics.

## Notes

### Resolved Decisions

**Q1 — Target scale: kilometre-scale world, 32–64 players, from the first playable.** Streaming and mip-based distant representation are phase-one requirements. This roughly doubles time to first playable compared with an arena-scale start, and is accepted deliberately: paging is the hardest thing to retrofit.

**Q2 — Persistence horizon: session-scoped.** The world resets between sessions. This bounds cumulative growth naturally and removes moderation-at-rest, backup, and long-horizon decay from scope. Note that it does *not* remove FR-022: a single long session still accumulates alterations without bound, so in-session compaction and budgets are still required.

**Q3 — Platform breadth: PC, console, and high-end mobile with crossplay.** *Narrowed 2026-08-04: mid-tier and low-tier mobile removed from scope.*

### Principal Risk: the scale/platform corner — reduced

Q1 and Q3 together still place this feature in a demanding corner: a kilometre-scale world with 64 concurrent players sets memory, bandwidth, and compute *ceilings* high, while mobile sets the *floor*. The discipline in C-002 stands — budgets are specified against the lowest supported class and allowed to scale up, never specified against PC and trimmed.

**What the mobile narrowing changed.** The original formulation of this risk was that the compute-shader raymarch is well established on PC and console but variable on *mid-tier* mobile, with no cheap fallback if it failed. Restricting to recent flagship phones largely retires that: the target hardware reliably provides Vulkan 1.1+ or Metal 3 with compute performance far closer to console than to the mass-market mobile floor. Validation on real hardware is still required before the rendering path is committed, but it is now an expected-pass measurement rather than a project-threatening gate.

The floor being higher also makes every other budget less punishing — memory, bandwidth, and detail radius are all set against a much more capable device.

A secondary consequence is unchanged: C-006 forbids device class from affecting outcomes. That constraint is easy to state and easy to violate accidentally — for example, if a mobile client's reduced draw distance also reduced the range at which it processed world updates, mobile players would be materially disadvantaged. Presentation tiering and simulation tiering must stay strictly separate.

### Related Artifacts

- `architecture-notes.md` — technical direction agreed in discussion, and the review of the superseded CTBS + CGVAVS proposal. Not a substitute for `/speckit-plan`; it is the input to it.
- `../../📘 Decoupled Voxel Rendering & Multiplayer Synchron.txt` — superseded proposal, retained for reference.
