# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** production composition and validation, primarily Kentridge application/session composition plus the shared standalone-player validation architecture. **No new generic gameplay API/Runtime module.**

## Implementation

1. Make Kentridge enter through #23 Application and #14 SessionOrchestration rather than directly constructing Campaign/Combat/Input/etc.
2. Retain Kentridge-specific world seed/content/sites/NPCs/cutscenes/encounter placement in Kentridge composition.
3. Remove scene-local service ownership (`CombatService`, `InputContextService`, raw key polling and equivalent alternate runtimes).
4. Exercise one representative chain through movement, interaction, progression, encounter/combat, inventory/loot and presentation.
5. In the combat portion, prove the production squad-beat model rather than a serial per-character turn substitute: Combat selects the active squad member, the player chooses one deliberate move, non-active members continue autonomous behavior, and at least one configured character interaction joins, redirects, or transforms/escalates an action/event already in progress.
6. Prefer a spatially readable interaction—movement/launch, projectile, impact/collision, ally augmentation, or destructible-world consequence—so the proof demonstrates event-driven combo grammar rather than only a status-triggered proc.
7. Present current/upcoming active members and the actionable combo opportunity through the production HUD/VFX path; presentation remains non-authoritative.
8. Add one real save -> teardown -> Continue -> restore round trip.
9. Drive the standalone player only through semantic/player inputs; validation may observe diagnostic state but may not mutate authority.
10. Use the repository's one shared built-player harness and module-local validation conventions.

## Dependencies

Production implementations of 01-23 as needed for the selected route, including the squad-beat/event-chain contracts in #01 and their HUD/VFX projections in #17/#22.

## Proof

Frontend -> New Game -> GameplayReady -> representative gameplay -> encounter -> authoritative squad beat -> one deliberate selected-member action -> visible multi-character event-driven combo -> combat/encounter resolution -> save/continue -> further gameplay, with no unhandled exceptions or test-only substitute runtimes. The combat proof must not require a player turn for every squad member.

## Do not build

No second top-level integration scene, privileged quest/combat setters, feature-specific generic harness code, or Kentridge-only combat rules that bypass system 01.
