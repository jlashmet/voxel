# 15 Game outcome & completion policy — implementation plan

**Target module:** `Assets/Game/Outcomes/Api` / `Runtime` (`Game.Outcomes.Api`, `Game.Outcomes.Runtime`).

## API

Outcome lifecycle (`Running`/`Resolved`), disposition, semantic `OutcomeRef`, resolution request/result, current outcome snapshot, and exactly-once `GameOutcomeResolved` event.

## Runtime

1. Implement single immutable/idempotent terminal result.
2. Accept terminal requests only from configured authoritative policy/composition; ordinary combat/defeat facts remain nonterminal unless mapped by content/policy.
3. Define deterministic handling of competing terminal requests according to authoritative processing order.
4. Expose snapshot/projection for persistence and replication through adapters.
5. Notify #14 of resolution; do not perform shutdown/UI/save actions directly.

## Dependencies

Consumes semantic facts/policy from Progression/Story/character/encounter composition; independent of presentation and transport.

## Tests / proof

Ordinary combat loss remains Running, configured campaign success resolves once, configured party defeat resolves when policy says, duplicate/late requests ignored, technical server shutdown creates no outcome.

## Do not build

No final-boss flags, scene transitions, score screens, save deletion, or network shutdown.
