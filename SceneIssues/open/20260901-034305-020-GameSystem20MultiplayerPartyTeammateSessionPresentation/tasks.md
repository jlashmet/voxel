# 20 Multiplayer party, teammate & session presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.SessionPresentation.Api` / `Game.SessionPresentation.Runtime`
**Execution rule:** rows are keyed by durable PartyMemberId, not sockets or GameObjects. Presentation reflects Sessions/Continuity/GameplayReady truth.

## API / model

- [x] **T20-001 — Inventory current lobby/teammate UI.** No existing connection-indexed lobby/HUD member-row path exists on the assigned baseline; Sessions already keeps transport handles runtime-private and Systems 17/23 can consume the new semantic seam.
- [ ] **T20-002 — Establish asmdefs.** Runtime consumes Sessions.Api, Continuity.Api, GameplayReplication.Api and Characters.Api only; no transport Runtime dependency.
- [ ] **T20-003 — Define stable member presentation snapshot.** Key by PartyMemberId and include PlayerSlot, CharacterId when bound, leader role, presence/recovery/readiness and display metadata.
- [ ] **T20-004 — Define session-level presentation state.** Capacity/start readiness/current lifecycle needed by frontend/HUD without exposing network internals.
- [ ] **T20-005 — Define semantic UI intents.** Ready/start/leave requests route to Sessions/Application APIs; no direct transport disconnect/start calls.
- [ ] **T20-006 — Define compact teammate-status projection.** HUD can consume stable health/presence/readiness references without sharing mutable screen state.

## Runtime / views

- [ ] **T20-010 — Project Sessions roster to stable rows.** Preserve row identity/order across updates based on PartyMemberId/PlayerSlot.
- [ ] **T20-011 — Merge Continuity state.** Interrupted/reconnecting/resynchronizing states update the same durable row instead of remove/re-add.
- [ ] **T20-012 — Merge GameplayReady state.** Connected and GameplayReady are visibly/distinctly represented.
- [ ] **T20-013 — Resolve controlled CharacterId display.** Character binding changes update semantic teammate presentation without relying on GameObject identity.
- [ ] **T20-014 — Route ready/start/leave intents.** Presentation only requests operations and displays returned/current truth.
- [ ] **T20-015 — Integrate frontend and HUD projections.** Rich party screen for system 23 and compact status for system 17 through small read-only contracts.
- [ ] **T20-016 — Rebuild after frontend navigation/reconnect.** Current Sessions/Continuity state reconstructs rows with no stale connection identity.
- [ ] **T20-017 — Replace raw network/lobby UI paths.** Remove direct socket-index/transport-event presentation once parity is reached.

## Verification

- [ ] **T20-020 — Stable-row reconnect test.** New connection id updates the same PartyMemberId row and preserves PlayerSlot/CharacterId.
- [ ] **T20-021 — Connected-vs-ready test.** UI cannot report GameplayReady merely because transport is connected.
- [ ] **T20-022 — Explicit-leave test.** Leave removes/updates row according to Sessions policy and differs from interruption state.
- [ ] **T20-023 — Multi-member ordering/identity tests.** Joining/leaving/reconnecting does not cross-wire rows or character bindings.
- [ ] **T20-024 — Frontend/HUD projection tests.** Both consume the same semantic source without mutating it.
- [ ] **T20-025 — Module-local built-player multiplayer visual validation.** Use shared harness where player-visible proof is needed.

## Cleanup / close

- [ ] **T20-030 — Remove socket/GameObject row identity.** Repository search for connection ids used as UI member keys.
- [ ] **T20-031 — Scope audit.** No chat, matchmaking browser, transport control or gameplay authority in SessionPresentation.
- [ ] **T20-032 — Close with continuity proof.** Teammate row survives transport replacement and always reflects durable identity/current semantic state.
