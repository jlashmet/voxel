# 24 Production-composed built-player vertical slice — implementation plan

## Acceptance and ownership

Prove FrontEnd -> New Game -> GameplayReady -> physical movement -> authored NPC/story progression -> WorldBuilder encounter -> player-input Combat/Vitality resolution -> WorldObject/Loot/Inventory pickup -> save -> ordered teardown -> Continue -> restored, live gameplay. Every required task and exact-SHA player gate remains binding.

Application owns the one Input/session lifecycle; Kentridge supplies named content and composition, not parallel authority. Loot uses WorldObjectRegistry, ItemPickupObject and WorldObjectLootAdapter. Persistence round-trips campaign, inventory, character, encounter and WorldObject state. Combat AI advances enemy turns only; the representative player acts through physical input.

## Current evidence and hypotheses

Product `bcb317a986661265aee5ade5155bdf0f523e54bd` includes master `ef475182b866eabfe8e1d1a39c82bf7810a03f49`. Exact request `7db111ceb16816ae3d480160c3229d8e5b0ec5c5`, run `34004591329`, is running repository-selected validation. Preserve that request; it does not validate subsequent regression additions.

Earlier compilation/Structures harness blockers were fixed or inherited from master. Runs `34000242054` and `34001710667` passed editor tests but failed the assembled MoveToDestination milestone. Direct NPC steering and then forward-until-exit steering did not establish traversal. Stop further speculative route fixes.

1. **Premature control handoff contributes to failure.** Code ordering allows the old readiness predicate to become true before the slice performs its physical opening release. The product now requires `_openingGameplayReleased`. Whether this fixes the persistent movement stall remains unproven.
2. **Input delivery or physical access is still wrong.** Existing logs lack the input/kinematic samples needed to discriminate these. Current access facts distinguish the doorway normal from a potentially diagonal exterior approach. Historical doorway test code is not evidence that this exact WorldBuilder-produced corridor is traversable.

Next discriminating experiment: a module-owned, exit-only built-player probe. It observes the production handoff, queues ordinary forward gamepad input, records the production input snapshot, device state, velocity, camera basis and position, and requires physical travel plus HasExitedPub. It may not teleport, change readiness, reflect private fields, bypass collision, or substitute a simplified runtime.

## Validation surface and cost

Add the probe scene/scenario under `Assets/Game/Composition/Kentridge/Playable/SceneRuntime/Validation/`. Reuse the canonical serialized production composition, generators, world, lighting and presentation; isolate the exercised route rather than reconstructing a fake pub. The pre-existing local scene tests encounter bindings, not opening control. Application and Input retain their owned validation scenes. Headless Kentridge Runtime, Persistence and Combat use owned EditMode behavioral coverage because they do not own scene presentation.

The probe is opt-in, samples once per second and uses a bounded 120-second scenario. No world, rendering, collision or performance budget changes. Canonical System24 integration remains required separately.

## Remaining gates

Inspect the running request's terminal result and durable player evidence. Validate the added regression on its exact feature SHA using the existing CI transport only after the current request terminates. Resolve demonstrated failures, complete every checklist item, inspect visual quality, close open -> closed, merge current master, then PR + auto-merge and verify closure on master.
