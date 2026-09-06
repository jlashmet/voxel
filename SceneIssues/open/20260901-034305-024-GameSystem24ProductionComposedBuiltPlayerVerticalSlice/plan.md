# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition plus shared standalone-player validation. No alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> physical movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Use public production boundaries and direct built-player evidence.

## Current evidence and selected fixes

Earlier exact runs compiled and passed editor validation but repeatedly stalled in `MoveToDestination`. Exact request `08274cda34b900a5bfe22a0a890f0306a42dddbc` / run `34008004416` was terminal **cancelled**, so it is not acceptance success. Its durable artifact proved physical Input System movement and public `HasExitedPub`, falsifying opening/input failure and isolating straight-line post-exit steering into generated geometry.

Selected route correction: generated WorldBuilder facts expose the assigned NPC site's immutable public `NetworkApproachDm` and architecture-projected `PublicEntranceDm`. The validation driver follows those semantic waypoints through ordinary player input before approaching the NPC; no coordinate constants, teleport, collision bypass, or authority mutation are used. Commits `a46f1596`, `2768ffe6`, and `5da005c0` implement the route contract and consumer.

Exact request `3030258dd2cb9095e84995d54c55c018ebcbd414` / run `34016877563` reached the destination but did not start the destination interaction. Commit `a0a8627221b94ed70643a0f579b5f1d859b683c4` moved the interaction pulse onto the same proven virtual gamepad used for movement/combat. Exact request `7dd2aac6afc1c753742076b0302d862c8e88bb6c` / run `34027162564` reproduced the same symptom, satisfying the two-fix stop rule and requiring a root-cause isolate.

Exact request `0ee4e4ee3db992c290ab5e88b4834ae9c36c2914` / run `34030268766` was terminal **cancelled** during repository-derived module validation, so it is not acceptance success. Its durable game-integration artifact provides the discriminating result: `SYSTEM24_INTERACTION_DIAGNOSTIC edge=true` repeats at player `(91.692,22.200,54.886)`, destination `(90.300,22.200,55.900)`, distance `1.722`, production range `2.500`, `objectiveActive=True`, and `destinationCutscene=False`. Therefore physical/semantic Interact delivery is falsified as the root cause. Static inspection shows `CampaignRuntime.InteractWithNpc` synchronously dispatches `NpcInteracted` before progression, so the remaining boundary is production nearby-conversation-NPC selection versus session/story resolution. Commit `18a1472d36d9b229476f46ddf591461ea9fc8e9c` extends only the System24 read-only diagnostic to record all eligible conversation NPC refs/distances and the nearest candidate; it does not issue commands or mutate authority.

T24-023 audit also found vitality absent from save/Continue. Production persistence now captures/restores current/max/defeated/revision and canonical validation compares saved/restored vitality; focused behavioral coverage exists in the SceneRuntime owned test assembly.

Current `origin/master` is `18845c608f34639ca6f1629250d2695123f9217b`; per the SceneIssue workflow it must be merged into the feature branch only after the assignment-specific exact-SHA gate is green.

## Remaining gates

Run the nearest-conversation-candidate isolate through one exact-head request on `ci-test/fixes/agent-2`; use that evidence to make only the proven root-cause fix. Then require repository-derived module validation, the owned opening-control player, vitality regressions, and canonical Kentridge route through every System24 milestone. A cancelled/failed run cannot satisfy a checkbox. On terminal green, inspect exact built-player screenshots directly and require production-quality presentation. Then complete every task, close open -> closed with resolution metadata, merge latest master into the feature branch, promote only through PR + auto-merge, monitor required `affected`, and verify the closed SceneIssue on `origin/master`.