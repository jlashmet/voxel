# 14. Game session & campaign orchestration

**Status:** Approved

## Purpose

Provide one authoritative composition/orchestration boundary for a running gameplay session without turning the existing `CampaignRuntime` into a god object.

`CampaignRuntime` remains responsible for semantic campaign progression, story-rule dispatch, quests/objectives, cutscenes, and campaign progress effects. System 14 sits above it and assembles the other authoritative gameplay runtimes into one coherent running session.

Conceptually:

`party/session + realized world + campaign content`

→ **Game Session Orchestrator**

→ characters / campaign / encounters / WorldObjects / inventory / combat

→ semantic results/events back into campaign/story and other domain consumers

## 1. Own the gameplay-run lifecycle

The orchestrator owns only high-level run readiness/lifecycle, conceptually:

`Uninitialized → Composing → Ready → Running → ShuttingDown`

It determines whether the authoritative gameplay graph has been assembled and may accept gameplay commands.

Victory/failure policy belongs to system 15 rather than this lifecycle.

## 2. Compose the runtime graph once

Given campaign content, realized WorldBuilder state, party/session information, and configured services, the orchestrator wires the authoritative runtime graph once.

Expected participants include the systems designed earlier where applicable:

- character runtime/registry (03)
- character AI (04)
- encounters (05)
- gameplay replication adapters (06)
- party/session identity (07/08)
- inventory/loot (09/10)
- quest/objective progression (11)
- encounter realization bindings (12)
- authoritative WorldObject interaction bridge (13)
- combat integration (01)
- vitality (02)

Individual scenes must not independently construct parallel versions of this graph.

## 3. New-game and resume paths converge

A new game and a resumed game should produce the same runtime graph.

- New game: compose the graph, initialize fresh authoritative state, then dispatch semantic new-game progression.
- Resume: compose the same graph, restore snapshots supplied by system 16, then enter running state without replaying one-shot new-game effects.

The orchestrator does not implement serialization/storage itself.

## 4. Route cross-system facts, not domain rules

The orchestrator coordinates narrow semantic boundaries between systems.

Example:

`character interaction intent`
→ system 13 executes authoritative WorldObject behavior
→ semantic result/fact
→ system 11 / campaign story observes relevant fact
→ content may request cutscene, encounter, objective progression, etc.

The orchestrator must not contain campaign-specific rules such as which lever starts which encounter.

## 5. Deterministic cross-system ordering

When one authoritative action can affect several systems, processing order must be explicit and deterministic rather than depending on Unity callback order.

This extends the existing campaign-runtime pattern where story dispatch, quest observation, and objective completion are deliberately sequenced.

## 6. Coordinate high-level control ownership

The orchestrator may coordinate temporary high-level activity/control restrictions such as:

- cutscene control versus normal player control
- session shutdown preventing new gameplay commands
- transition into/out of constrained encounter contexts where necessary

It must not absorb the domain rules of combat, AI, cutscenes, or encounters into a giant `GameMode` implementation.

## 7. Feed semantic world/gameplay facts into campaign progression

Spatial, interaction, encounter, quest, and other authoritative systems produce semantic facts.

The orchestrator/composition layer routes relevant facts into campaign/story through narrow contracts. Story remains independent of concrete runtime implementations.

## 8. Keep campaign sequencing in authored content

Rules such as:

- new game → intro cutscene
- cutscene complete → start objective
- interact with NPC → play cutscene
- cutscene complete → join party/grant progression effect

remain in campaign/story content.

System 14 executes and connects a campaign; it does not hardcode any specific campaign flow.

## 9. Integrate with the existing authoritative server loop

Networking/transport remains responsible for authoritative ticks, replication, prediction, reconciliation, and connection handling.

System 14 participates in the authoritative gameplay update opportunity and orders only the gameplay/session runtimes that require coordinated updates.

It must not create a second competing game loop.

## 10. Explicit startup and teardown

No gameplay subsystem should act against a half-composed world.

Startup establishes required bindings/registries before entering `Running`.

Shutdown prevents new commands and tears down runtime ownership in a defined order so stale callbacks, actors, WorldObject presentation, encounters, or registries cannot leak into the next run.

## 11. Expose orchestration state only

The orchestrator may expose session-level state such as lifecycle/readiness and high-level control ownership.

It must not become a universal query surface for health, inventory, quests, combat targets, or other domain-owned state.

## 12. Keep the core engine-independent

The orchestration state machine and composition contracts should remain deterministic/testable without Unity scene objects.

Unity may provide a thin bootstrap/host that creates, ticks, and disposes the orchestrator.

## Reuse / integration proof

### New run

1. Compose a small realized world and campaign.
2. Establish authoritative characters, WorldObjects, inventory, campaign, and encounter bindings.
3. Start new-game progression.
4. Complete a real interaction/progression step.
5. Trigger and resolve an encounter.
6. Verify campaign/story observes the semantic result through the configured boundaries.

### Resume

1. Compose the same runtime graph from the same content/realized world contract.
2. Restore authoritative snapshots supplied by system 16.
3. Verify one-shot completed content does not replay.
4. Verify stable character/WorldObject identities and progression continue from restored state.

The same orchestrator must support both paths.

## Out of scope

- campaign/story content and sequencing rules
- combat mechanics (01)
- vitality/character/AI domain behavior (02–04)
- encounter lifecycle (05)
- networking/party/reconnect mechanics (06–08)
- inventory/loot behavior (09–10)
- quest/objective progression logic (11)
- WorldBuilder realization (12)
- WorldObject behavior (13)
- victory/failure policy (15)
- serialization/storage (16)
- HUD/UI/audio/VFX (17–23)
- full-game pacing/progression design (26)

## Architectural constraints

- The orchestrator coordinates domains; it does not own their internal rules/state.
- Cross-system communication is semantic and deterministic.
- Campaign-specific policy stays in campaign/content composition.
- Shared orchestration remains engine-independent where practical.
- New-game and resume boot paths converge on the same authoritative runtime graph.
- Reuse the existing campaign/story/quest/cutscene runtimes instead of replacing them.
