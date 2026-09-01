# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** production composition and validation, primarily Kentridge application/session composition plus the shared standalone-player validation architecture. **No new generic gameplay API/Runtime module.**

## Implementation

1. Make Kentridge enter through #23 Application and #14 SessionOrchestration rather than directly constructing Campaign/Combat/Input/etc.
2. Retain Kentridge-specific world seed/content/sites/NPCs/cutscenes/encounter placement in Kentridge composition.
3. Remove scene-local service ownership (`CombatService`, `InputContextService`, raw key polling and equivalent alternate runtimes).
4. Exercise one representative chain through movement, interaction, progression, encounter/combat, inventory/loot and presentation.
5. Add one real save -> teardown -> Continue -> restore round trip.
6. Drive the standalone player only through semantic/player inputs; validation may observe diagnostic state but may not mutate authority.
7. Use the repository's one shared built-player harness and module-local validation conventions.

## Dependencies

Production implementations of 01-23 as needed for the selected route.

## Proof

Frontend -> New Game -> GameplayReady -> representative gameplay -> save/continue -> further gameplay, with no unhandled exceptions or test-only substitute runtimes.

## Do not build

No second top-level integration scene, privileged quest/combat setters, or feature-specific generic harness code.
