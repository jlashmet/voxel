# 13 Authoritative world-object interaction — implementation plan

**Target module:** `Assets/Game/WorldObjects/Api` / `Runtime` (`Game.WorldObjects.Api`, `Game.WorldObjects.Runtime`). Migrate/generalize existing authoritative world-object behavior rather than duplicating it.

## Acceptance / ownership

WorldObjects owns authoritative object identity, semantic state, validation, deterministic behavior dispatch, capture/restore, and accepted interaction facts. Characters supplies `CharacterId`; Loot and Progression remain downstream adapters. No UI/input authority, inventory mutation, scene-specific IDs/policy, Unity objects, or parallel interaction runtime belong in generic WorldObjects.

Required proof: multiple behavior types through one runtime, explicit invalid actor/range/state/capability rejection, deterministic repeated interaction, save/restore without side-effect replay, independent non-Kentridge reuse, repository-selected dependent module tests, and the required standalone application gate.

## Current result / hypotheses

Implementation and focused regressions are complete. Exact-SHA run `33823479614` correctly resolved source `208ffa4068948e3e559ef61c2416ff1fb2709f21`, then both module validation and standalone-player build failed at compile time with CS0012 in `WorldObjectProgressionAdapter`: `Game.Progression.Runtime` consumes `WorldInteractionFact.ActorId` (`CharacterId`) but its asmdef lacks `Game.Characters.Api`.

Hypothesis A: missing direct assembly reference is the sole compiler blocker. Hypothesis B: the adapter is exposing an unintended WorldObjects→Characters ownership leak. The existing contract already intentionally defines actor identity as `CharacterId`, and WorldObjects Runtime directly references Characters Api, so the discriminating evidence selects A; adding the consumer-side direct asmdef reference preserves the planned dependency direction.

## Selected fix / remaining gates

Add only `Game.Characters.Api` to `Game.Progression.Runtime` references; do not change interaction contracts or ownership. Re-run exact-SHA targeted CI on the new feature SHA. If green, complete T13-025 and closure metadata, move only this SceneIssue `open/`→`closed/`, merge current `origin/master` into `fixes/agent-2`, then promote only through PR + auto-merge and the required `affected` PR gate.
