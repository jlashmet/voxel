# 13 Authoritative world-object interaction — implementation plan

**Target module:** `Assets/Game/WorldObjects/Api` / `Runtime` (`Game.WorldObjects.Api`, `Game.WorldObjects.Runtime`). Migrate/generalize existing authoritative world-object behavior rather than duplicating it.

## Acceptance / ownership

WorldObjects owns authoritative object identity, semantic state, validation, deterministic behavior dispatch, capture/restore, and accepted interaction facts. Characters supplies `CharacterId`; Loot and Progression remain downstream adapters. No UI/input authority, inventory mutation, scene-specific IDs/policy, Unity objects, or parallel interaction runtime belong in generic WorldObjects.

Required proof: multiple behavior types through one runtime, explicit invalid actor/range/state/capability rejection, deterministic repeated interaction, save/restore without side-effect replay, independent non-Kentridge reuse, repository-selected dependent module tests, and the required standalone application gate.

## Current result / hypotheses

Implementation and focused regressions are complete. Exact-SHA run `33823479614` correctly resolved source `208ffa4068948e3e559ef61c2416ff1fb2709f21`, then both module validation and standalone-player build failed at compile time with CS0012 in `WorldObjectProgressionAdapter`: `Game.Progression.Runtime` consumes `WorldInteractionFact.ActorId` (`CharacterId`) but its asmdef lacked `Game.Characters.Api`.

Hypothesis A: missing direct assembly reference was the sole compiler blocker. Hypothesis B: the adapter exposed an unintended WorldObjects→Characters ownership leak. The existing contract already intentionally defines actor identity as `CharacterId`, and WorldObjects Runtime directly references Characters Api, so the discriminating evidence selected A; adding the consumer-side direct asmdef reference preserves the planned dependency direction.

The corrective feature commit is `7fb196bb8360f703a5f251674d1b134dec76347e`. Exact-SHA request commit `1df6d1aeb304224f8829115ebcfc77c65e7dc98b` directly parents that product SHA, and repository validation run `33827364421` completed successfully.

## Validation-scene exception

The WorldObjects/Progression runtime seam exercised by this feature is pure headless/domain behavior with no meaningful player-visible scene behavior. `Game.Progression.Runtime` declares `noEngineReferences: true`, and the WorldObjects regressions are scene-free semantic tests. Per the current feature guide, module-local EditMode/unit coverage is therefore the focused validation surface; creating a module-local scene solely for this seam would not exercise additional behavior. The repository exact-SHA standalone `KentridgePlayableSlice` gate remains the application-integration validation.

## Closure / promotion

All implementation and exact-SHA validation gates are complete. Close only this SceneIssue from `open/` to `closed/`, merge current `origin/master` into `fixes/agent-2`, then promote only through PR + auto-merge and the required PR `affected` gate. Do not push the feature head directly to `master`.
