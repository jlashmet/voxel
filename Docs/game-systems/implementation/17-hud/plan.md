# 17 Production gameplay HUD & semantic presentation — implementation plan

**Target module:** `Assets/Game/Hud/Api` / `Runtime` (`Game.Hud.Api`, `Game.Hud.Runtime`). API is engine-neutral read/presentation contracts; Runtime may contain Unity client presenters/views.

## API

Local-player HUD read model/presenter inputs: controlled CharacterId, vitality, interaction prompt semantics, encounter/combat state needed for display, tracked progression summaries, readiness/connection state references. Use semantic action ids, not physical bindings.

## Runtime

1. Build snapshot/event adapters from replicated/current semantic APIs.
2. Create independent presenters for vitality, interaction, combat/encounter and other approved HUD concerns.
3. Resolve local binding/glyph text through Input binding-presentation seam.
4. Rebuild from current state after reconnect/restore; transient effects dedupe by semantic event identity.
5. Replace prototype/Kentridge hardcoded GUI labels and prompts.

## Dependencies

06 replicated state, 02/03/05 APIs, 11 Progression API, Input API/binding presentation. No authority mutation.

## Tests / proof

Presenter unit tests without Unity, reconnect rebuild, binding changes update prompts, two local players resolve their own state, built-player visual validation.

## Do not build

No inventory journal/party screen ownership, gameplay authority, or hardcoded key names.
