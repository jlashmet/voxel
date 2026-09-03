# 17 Production gameplay HUD & semantic presentation — implementation plan

**Target module:** `Assets/Game/Hud/Api` / `Runtime` (`Game.Hud.Api`, `Game.Hud.Runtime`). API is engine-neutral read/presentation contracts; Runtime may contain Unity client presenters/views.

## API

Local-player HUD read model/presenter inputs: controlled CharacterId, vitality, interaction prompt semantics, encounter/combat state needed for display, tracked progression summaries, readiness/connection state references. Use semantic action ids, not physical bindings.

For squad-beat combat, the HUD consumes Combat-owned read models for:

- the local player's system-selected active squad member for the current beat;
- a small authoritative horizon of upcoming squad members so sequencing can be planned;
- semantic action choices available to the current active member and beat timing/commit state;
- transient combo opportunities and eligible/expected participants when Combat can expose them semantically;
- teammate beat/commit information required for cooperative timing without leaking transport identities.

Predicted combo previews are presentation only. They must be visibly distinguishable from confirmed outcomes and may never mutate or become an alternate source of Combat truth.

## Runtime

1. Build snapshot/event adapters from replicated/current semantic APIs.
2. Create independent presenters for vitality, interaction, combat/encounter and other approved HUD concerns.
3. Make the combat affordance communicate **system chooses WHO; player chooses WHAT**: current member, upcoming sequence, one deliberate move, and current beat state must be readable without presenting 20–30 individual turns.
4. Present combo opportunities as relationships to actions/events in progress—trajectory, impact, movement, projectile, ally/world interaction and likely join/redirect/transform links—rather than as a wall of status-proc icons.
5. Resolve local binding/glyph text through Input binding-presentation seam.
6. Rebuild from current state after reconnect/restore; transient effects dedupe by semantic event identity.
7. Replace prototype/Kentridge hardcoded GUI labels and prompts.

## Dependencies

06 replicated state, 01 Combat API, 02/03/05 APIs, 11 Progression API, Input API/binding presentation. No authority mutation.

## Tests / proof

Presenter unit tests without Unity, reconnect rebuild, binding changes update prompts, two local players resolve their own state, active/upcoming beat projection, non-active members do not receive deliberate-action controls, predictive previews remain non-authoritative, and built-player visual validation proves the player can identify the current actor, next actor and useful combo opportunity quickly.

## Do not build

No inventory journal/party screen ownership, gameplay authority, hardcoded key names, HUD-owned turn sequencing, or exhaustive generic status/reaction registry.
