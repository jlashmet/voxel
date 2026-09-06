# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition plus shared standalone-player validation. No alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> physical movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Use public production boundaries and direct built-player evidence.

## Current evidence and selected fixes

Earlier exact runs compiled and passed editor validation but repeatedly stalled in `MoveToDestination`. Exact request `08274cda34b900a5bfe22a0a890f0306a42dddbc` / run `34008004416` was terminal **cancelled**, so it is not acceptance success. Its durable artifact is diagnostic only: the owned `KentridgeOpeningControlValidation` player physically received Input System movement and reached public `HasExitedPub`, while canonical System24 later stalled around `(101.2,22.0,55.9)`, about 10.9 m from the destination NPC. This falsifies opening/input failure and isolates straight-line post-exit steering into generated geometry.

Selected route correction: generated WorldBuilder facts expose the assigned NPC site's immutable public `NetworkApproachDm` and architecture-projected `PublicEntranceDm`. The validation driver follows those semantic waypoints through ordinary player input before approaching the NPC; no coordinate constants, teleport, collision bypass, or authority mutation are used. Commits `a46f1596`, `2768ffe6`, and `5da005c0` implement the route contract and consumer.

Exact request `3030258dd2cb9095e84995d54c55c018ebcbd414` / run `34016877563` attempt 2 passed persistent editor/module tests and many module players, but the canonical game-integration player repeatedly reached the destination and re-entered `MoveToDestination` / `WaitDestinationInteraction` without producing the NPC interaction milestone before the run was cancelled. This is a product validation failure, not runner admission/editor/native-import infrastructure. The production `UnityPlayerInputReader` binds `Interact` to gamepad South as well as keyboard E; movement on the driver's virtual gamepad is already proven in the same exact player. Commit `a0a8627221b94ed70643a0f579b5f1d859b683c4` therefore makes the System24 interaction pulse use that same proven gamepad device instead of the separate virtual keyboard path. No gameplay authority or interaction shortcut was added.

T24-023 audit also found vitality absent from save/Continue. Production persistence now captures/restores current/max/defeated/revision and canonical validation compares saved/restored vitality; focused behavioral coverage exists in the SceneRuntime owned test assembly.

`master` `356b2e0e4d2818901c73bbc6b1788f8d6850356d` is integrated by merge `6d1357f572f4ba48a4bf7d3278101dd2235913f6`. Accidental placeholder files added after that merge were removed; they are not part of the product tree.

## Remaining gates

Issue one exact-head request through `ci-test/fixes/agent-2` from the current feature head. Require repository-derived module validation, the owned opening-control player, vitality regressions, and canonical Kentridge route through every System24 milestone. A cancelled/failed run cannot satisfy a checkbox. On terminal green, inspect exact built-player screenshots directly and require production-quality presentation. Then complete every task, close open -> closed with resolution metadata, recheck/merge latest master if needed, promote only through PR + auto-merge, monitor required `affected`, and verify the closed SceneIssue on `origin/master`.
