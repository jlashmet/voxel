# 26 Authored full-run campaign progression & completion — implementation plan

**Ownership:** production campaign Story/Progression content under composition/content assemblies; reuse Story, System11 Progression, System15 Outcomes, System16 persistence, and Systems14/23. **No generic GameLoop/Chapter runtime.**

## Observed behavior / acceptance

`KnownOpeningCampaignContent` enters through `NewGame` and reaches the Kentridge/Medrare opening only. It already uses unified Progression and persistent cutscene/party/spell state, but has no later campaign route or terminal outcome. Recovered Mounting Force evidence contains verified positive scene-dependency chains beyond the opening and a final Logan-castle chain; inferred quest labels/filenames are not chronology. Detailed provenance and authored bridges are recorded in `route-evidence.md`.

Acceptance is one normal New Game route crossing multiple recovered consequences to immutable `GameOutcomeResolved`, with optional content non-gating, mid-run restore, built-player proof, and shared multiplayer observation when System25 is available.

## Hypotheses / discriminating result

1. **Preferred:** existing Story + Progression can own the route with only two narrow missing semantics: encounter-resolution input and an Outcome-condition effect routed to System15. Inspecting current APIs supports this: System15 already has `OutcomePolicyRouter`, Encounters already emits semantic resolution facts, and Progression already owns objective truth.
2. **Rejected:** a campaign chapter/phase runtime is needed. No acceptance gap requires phase state; adding it would duplicate Story/Progression and violate the feature non-goal.

## Selected implementation

- Refactor opening composition into reusable plain slice helpers while preserving existing `Build` behavior; no chapter interface unless repeated concrete slices prove one is necessary.
- Add later authored slices following the canonical evidence spine: opening/church -> authored Rorik bridge -> verified Rorik/Moordell/Rossdam edges -> authored Logan bridge -> verified castle terminal chain.
- Source battle completion only from Encounters; Story may observe it but never mutate combat. Terminal Story effect observes a configured `OutcomeConditionRef`; System15 performs the exactly-once resolution.
- Fast engine-independent route test drives real public domain/Story/Progression actions and a mid-run restore. Full built-player route uses Systems23/14/16 and milestone waits.

## Current state / gates

Baseline was synced to master `6bd0992630ae27f2e30ebc32d65ba098cf987d25`; evidence commit begins at `1c1fe510143fd8e8c266fc2421c6cac1709145f6`. System25 multi-process harness is not yet on master, so T26-043 is an external prerequisite; do not substitute a parallel transport. Remaining gates: implementation, module-owned tests/validation, exact-SHA targeted CI, current-master resync, closure bookkeeping, PR `affected` gate, auto-merge.
