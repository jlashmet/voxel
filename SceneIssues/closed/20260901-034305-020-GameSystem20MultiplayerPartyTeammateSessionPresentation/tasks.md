# 20 Multiplayer party, teammate & session presentation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.SessionPresentation.Api` / `Game.SessionPresentation.Runtime`
**Execution rule:** rows are keyed by durable PartyMemberId, not sockets or GameObjects. Presentation reflects Sessions/Continuity/GameplayReady truth.

## API / model

- [x] **T20-001 — Inventory current lobby/teammate UI.** No connection-indexed lobby/HUD member-row path exists on current master; Sessions keeps transport handles runtime-private.
- [x] **T20-002 — Establish asmdefs.** Runtime references only SessionPresentation.Api plus Sessions.Api, Continuity.Api, GameplayReplication.Api and Characters.Api; no transport/runtime dependency.
- [x] **T20-003 — Define stable member presentation snapshot.** `PartyMemberPresentationSnapshot` is keyed by PartyMemberId and includes PlayerSlot, CharacterId, leadership, local marker, semantic connection/recovery/readiness and display metadata.
- [x] **T20-004 — Define session-level presentation state.** Rich snapshot exposes session id, capacity, lifecycle and semantic `CanStart`.
- [x] **T20-005 — Define semantic UI intents.** Ready/start/leave intents route only to Sessions-owned application commands.
- [x] **T20-006 — Define compact teammate-status projection.** HUD snapshot reuses durable member/CharacterId/presence/readiness facts and owns no mutable screen state.

## Runtime / views

- [x] **T20-010 — Project Sessions roster to stable rows.** Projector sorts by PlayerSlot then PartyMemberId and is stateless across captures.
- [x] **T20-011 — Merge Continuity state.** Interrupted/reconnecting/resynchronizing/expired/left map onto the same durable PartyMemberId row.
- [x] **T20-012 — Merge GameplayReady state.** Connected, explicit user-ready and replication GameplayReady remain distinct fields/states.
- [x] **T20-013 — Resolve controlled CharacterId display.** Character rebinding updates the same durable row; regression proves no GameObject identity is involved.
- [x] **T20-014 — Route ready/start/leave intents.** `SessionPresentationIntentRouter` forwards to Sessions application commands; presentation does not mutate authority directly.
- [x] **T20-015 — Integrate frontend and HUD projections.** Small rich `IPartyScreenPresentationQuery` and compact `ITeammateHudPresentationQuery` contracts consume the same semantic source. Systems 17/23 are not on current master, so no nonexistent consumer was modified.
- [x] **T20-016 — Rebuild after frontend navigation/reconnect.** A new projector reconstructs current semantic state from Sessions/Continuity/GameplayReplication with no transport history; regression passed.
- [x] **T20-017 — Replace raw network/lobby UI paths.** Inventory confirmed no raw socket/transport-indexed party UI path exists to migrate; the new seam prevents future consumers from requiring one.

## Verification

- [x] **T20-020 — Stable-row reconnect test.** `Reconnect_UpdatesSameDurableRowWithoutChangingSlotOrCharacter` passed; built-player log also preserved `party:bravo`, slot 1, `character:bravo` through reconnect.
- [x] **T20-021 — Connected-vs-ready test.** `Connected_DoesNotImplyGameplayReady` passed; module capture visibly distinguishes presence, explicit ready and synchronization.
- [x] **T20-022 — Explicit-leave test.** `ExplicitLeave_RemovesRosterRowWhileInterruptionKeepsDurableRow` passed.
- [x] **T20-023 — Multi-member ordering/identity tests.** Ordering/binding, reconnect and character-rebinding regressions passed without row cross-wiring.
- [x] **T20-024 — Frontend/HUD projection tests.** `PartyScreenAndHud_ConsumeSameSemanticRows` passed; snapshots defensively copy their rows.
- [x] **T20-025 — Module-local built-player multiplayer visual validation.** Exact workflow `33868484121` ran `SessionPresentationValidation` successfully (59.1s), captured four 1280x720 frames, and logged initial projection, stable reconnect, and `ReadyToStart canStart=True`; canonical Kentridge standalone validation also passed (100.9s).

## Cleanup / close

- [x] **T20-030 — Remove socket/GameObject row identity.** Final diff contains no transport/connection/GameObject member-key dependency in SessionPresentation API/runtime; GameObject usage is confined to validation scene setup, not row identity.
- [x] **T20-031 — Scope audit.** Diff adds only SessionPresentation semantic projection, Sessions semantic command/query seam, regressions/validation and SceneIssue evidence; no chat, matchmaking browser, transport control or gameplay authority.
- [x] **T20-032 — Close with continuity proof.** Exact source `1312f423d4a92a3948404eaf415764eb425952a6` passed transport `9e8cd8387e5447356cad2059070a88974949647f`, run `33868484121`; durable member identity survived reconnect/rebuild and current semantic state was reflected in rich and compact views.
