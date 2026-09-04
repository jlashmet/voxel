# 13 Authoritative world-object interaction — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.WorldObjects.Api` / `Game.WorldObjects.Runtime`
**Execution rule:** WorldObjects owns authoritative interactive object identity/state/behavior dispatch. Input/UI only express intent; Loot/Progression react through semantic adapters.

## Baseline / API

- [x] **T13-001 — Inventory current world-object behaviors.** Repository searches for `Input.GetKey*`, mouse-button polling, `KeyCode.E`, `Physics.Raycast`, `Interact`, door/nested-subscene helpers, and the existing WorldObjects/Loot contracts found no competing production interaction authority on current master; GameSystem10 exposes validation/loot semantics only.
- [x] **T13-002 — Establish asmdefs and migration boundary.** Added `Game.WorldObjects.Runtime` with only `Game.WorldObjects.Api` + `Game.Characters.Api` dependencies; Loot and Progression integrations live in their owning consumer modules.
- [x] **T13-003 — Define stable `WorldObjectId`.** Preserved the existing ordinal, serialization-safe `WorldObjectId` contract and original failure enum values for GameSystem10 compatibility.
- [x] **T13-004 — Define semantic object state snapshot.** `WorldObjectStateSnapshot` carries stable id, behavior kind, enabled/state code, and revision without Unity/scene objects.
- [x] **T13-005 — Define interaction intent/context.** `WorldInteractionContext` carries semantic `CharacterId`; `InteractionClickedProcessor` translates sender Steam binding and co-located candidate selection without Camera/RaycastHit/GameObject types.
- [x] **T13-006 — Define interaction result/fact and rejection reasons.** Added explicit no-target, invalid-payload, missing-inventory, inventory-rejected semantics while preserving existing actor/object/range/permission/state/capability failures; accepted transitions emit `WorldInteractionFact`.
- [x] **T13-007 — Define behavior capability/handler seam.** `IWorldObjectBehavior` owns semantic interaction/capture/restore while generic WorldObjects depends only on `IWorldItemPickupTransfer` for the pickup side-effect seam.

## Runtime / migration

- [x] **T13-010 — Implement authoritative object registry/binding.** `WorldObjectRegistry` rejects duplicate ids, returns co-located candidates sorted by `WorldObjectId`, and provides ordered capture/restore.
- [x] **T13-011 — Centralize interaction validation.** `InteractionClickedProcessor` resolves `CharacterBinding("steam", senderSteamId)` through `ICharacterQuery`, validates object existence and exact semantic co-location, and returns explicit failures.
- [x] **T13-012 — Dispatch behavior deterministically.** Only the lowest-id co-located candidate is attempted; pickup disables only after transfer success, doors toggle deterministically, nested subscenes reject invalid state, and repeated consumed pickup requests reject without duplicate side effects.
- [x] **T13-013 — Emit semantic state-change/interaction facts.** The processor publishes exactly one fact after a successful transition and no fact after rejection; composite/null sink seams keep downstream ownership external.
- [x] **T13-014 — Add Loot adapter seam.** `WorldObjectLootAdapter` uses the existing `IInventoryTransactions` authority plus explicit `CharacterInventoryBindings`; generic WorldObjects never imports Inventory or invents inventory ids.
- [x] **T13-015 — Add Progression/Story interaction adapter seam.** `WorldObjectProgressionAdapter` translates accepted world facts into `IProgressionFactSink` facts without WorldObjects depending on Progression.
- [x] **T13-016 — Add capture/restore and replication projection seams.** Registry/object snapshots are ordered semantic current-state projections; restore validates identity/kind/state and does not replay pickup transfer side effects.
- [x] **T13-017 — Migrate raw interaction input.** Required audit found no current production `E`/raw-key/mouse/raycast-to-behavior authority path to migrate; the new server-side processor consumes semantic sender/candidate state only.

## Verification

- [x] **T13-020 — Two-behavior reuse test.** EditMode coverage registers three pickups, three doors, and three nested-subscene toggles through one registry; deterministic multi-target selection is also covered.
- [x] **T13-021 — Validation rejection tests.** Coverage includes unknown requester, unknown object, out-of-range object, no target, unsupported capability, invalid payload, missing inventory, destination rejection, consumed/stale pickup state, and invalid nested state.
- [x] **T13-022 — Repeated interaction/state conflict tests.** Door/nested repeated transitions are deterministic; pickup second interaction rejects and neither inventory nor Progression is invoked twice.
- [x] **T13-023 — Snapshot/restore test.** Pickup, door, nested-subscene, and ordered registry state round-trip; consumed pickup restore explicitly proves no transfer replay.
- [x] **T13-024 — Independent non-Kentridge fixture.** `Game.WorldObjects.Tests` is a scene-free semantic fixture using independent `fixture:*` ids/positions and no Kentridge composition dependency.
- [ ] **T13-025 — Run automatic WorldObjects and dependent Loot/Progression tests.** Exact-SHA run `33823479614` resolved source `208ffa4068948e3e559ef61c2416ff1fb2709f21` correctly but failed before tests because `Game.Progression.Runtime` omitted the direct `Game.Characters.Api` reference required by `WorldInteractionFact.ActorId`. Fix the asmdef dependency, then rerun repository exact-SHA targeted CI; do not check this item until required module validation and standalone application validation are green.

## Cleanup / close

- [x] **T13-030 — Remove scene-object authoritative identity and direct behavior shortcuts.** Repository-wide search found no existing production raw-input/raycast/interaction shortcut to remove; no duplicate helper was introduced.
- [x] **T13-031 — Boundary audit.** Generic WorldObjects owns no UI prompts, Inventory mutation, quest policy, named scene/object policy, Unity engine object, or external Runtime dependency.
- [x] **T13-032 — Close with single-owner proof.** The production semantic entry point is `InteractionClickedProcessor` -> sorted `IWorldObjectRegistry` -> one `IWorldObjectBehavior`; Loot and Progression attach only through side-effect/fact adapters and do not process clicks independently.
