# 26 Authored full-run campaign progression & completion — implementation plan

**Target ownership:** production campaign Story/Progression content under composition/content assemblies, reusing existing Story runtime and #11 Progression. **No new generic GameLoop API/Runtime.**

## Implementation

1. Recover/author an evidence-backed production route beyond the current known opening using stable semantic sites/NPCs/encounters/objectives/cutscenes.
2. Decompose campaign content into manageable authored slices rather than one giant `MainCampaign.Build`; introduce a reusable chapter abstraction only if repetition demonstrates it.
3. Extend Story/Progression semantic vocabulary only when a concrete route needs a missing fact/condition/effect (for example EncounterCompleted); route through owning APIs.
4. Connect the final authored terminal condition to #15 `GameOutcome` rather than final-boss/scene flags.
5. Keep optional content optional; completion means at least one valid authored route, not exhausting every recovered map/quest.
6. Add a deterministic headless semantic route test from NewGame to GameOutcomeResolved.
7. Add slower built-player full-run proof through #23/#14 and a mid-run #16 restore; the driver performs player intents and may not call completion setters.

## Dependencies

11 Progression, existing Story/Campaign/Cutscenes, 14 SessionOrchestration, 15 Outcomes, 16 Persistence, production domain facts.

## Proof

A real semantic path from normal new-game startup through multiple authored gameplay consequences to one authoritative terminal outcome, preserving progression across restore and multiplayer replication.

## Do not build

No generic chapter/game-state/pacing manager, inferred campaign order from filenames, per-player campaign progress, or replacement Story/Quest engines.
