# 20 Multiplayer party, teammate & session presentation — implementation plan

**Target module:** `Assets/Game/SessionPresentation/Api` / `Runtime` (`Game.SessionPresentation.Api`, `Game.SessionPresentation.Runtime`).

## API

Party/session presentation snapshot keyed by PartyMemberId, including PlayerSlot, controlled CharacterId when available, readiness/presence/recovery state and display metadata. No transport connection ids in view identity.

## Runtime

1. Project Sessions + Continuity + GameplayReady state into stable teammate rows.
2. Preserve row identity across transport reconnects.
3. Expose compact teammate status for #17 HUD and richer screen model for #23 Application.
4. Route ready/start/leave requests back through semantic Sessions/Application intents.
5. Rebuild from current state after reconnect and frontend navigation.

## Dependencies

07 Sessions, 08 Continuity, 06 readiness, 03 Character binding.

## Tests / proof

Reconnect preserves row, connection vs GameplayReady displayed distinctly, explicit leave removal, multi-player ordering/identity, built-player validation.

## Do not build

No chat, matchmaking browser, transport UI, or gameplay authority.
