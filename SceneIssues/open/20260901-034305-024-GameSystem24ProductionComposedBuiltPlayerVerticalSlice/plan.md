# 24 Production-composed built-player vertical slice — implementation plan

**Ownership:** Kentridge Application/session composition plus shared standalone-player validation. No alternate authority, privileged gameplay setters, or second top-level integration scene.

## Acceptance

Prove FrontEnd -> New Game -> GameplayReady -> physical movement/NPC/story -> WorldBuilder encounter -> player-input Combat/Vitality -> WorldObject/Loot/Inventory -> save -> ordered teardown -> Continue -> equivalent restored state -> further gameplay. Use public production boundaries and direct built-player evidence.

## Current evidence and selected fixes

Earlier exact runs compiled and passed editor validation but repeatedly stalled in `MoveToDestination`. Exact request `08274cda34b900a5bfe22a0a890f0306a42dddbc` / run `34008004416` was terminal **cancelled**, so it is not acceptance success. Its durable artifact proved physical Input System movement and public `HasExitedPub`, falsifying opening/input failure and isolating straight-line post-exit steering into generated geometry.

Selected route correction: generated WorldBuilder facts expose the assigned NPC site's immutable public `NetworkApproachDm` and architecture-projected `PublicEntranceDm`. The validation driver follows those semantic waypoints through ordinary player input before approaching the NPC; no coordinate constants, teleport, collision bypass, or authority mutation are used. Commits `a46f1596`, `2768ffe6`, and `5da005c0` implement the route contract and consumer.

Exact request `3030258dd2cb9095e84995d54c55c018ebcbd414` / run `34016877563` reached the destination but did not start the destination interaction. Commit `a0a8627221b94ed70643a0f579b5f1d859b683c4` moved the interaction pulse onto the same proven virtual gamepad used for movement/combat. Exact request `7dd2aac6afc1c753742076b0302d862c8e88bb6c` / run `34027162564` reproduced the same symptom, satisfying the two-fix stop rule and requiring a root-cause isolate.

Exact request `0ee4e4ee3db992c290ab5e88b4834ae9c36c2914` / run `34030268766` was terminal **cancelled** during repository-derived module validation, so it is not acceptance success. Its durable game-integration artifact proved `SYSTEM24_INTERACTION_DIAGNOSTIC edge=true` at distance `1.722` inside the `2.500` m production range with the travel objective active, falsifying physical/semantic Interact delivery as the root cause. Static inspection showed `CampaignRuntime.InteractWithNpc` synchronously dispatches `NpcInteracted` before progression, leaving nearby conversation-target selection as the discriminating boundary.

Exact request `ede26202663a49f0de26974d3534a043171f2285` / run `34136440561` was also terminal **cancelled**, so it is not acceptance success. Its durable game-integration artifact identifies the root cause: at player `(91.682,22.200,54.889)`, the destination NPC is `1.712` m away, while eligible conversation NPC `awon` is only `1.011` m away (`medrare` is `1.742` m). Production therefore correctly selects `awon` as the nearest conversation target; the validation driver was stopping too far from its intended authored NPC. Commit `047d8ddf7cf3b0abc31991083b353f3bf9f6756d` narrows only the validation approach threshold from `1.75` m to `0.50` m so ordinary movement brings the player physically nearest to the intended destination before the same semantic Interact input. No target override, teleport, collision bypass, or gameplay authority shortcut was added.

T24-023 audit also found vitality absent from save/Continue. Production persistence now captures/restores current/max/defeated/revision and canonical validation compares saved/restored vitality; focused behavioral coverage exists in the SceneRuntime owned test assembly.

Current `origin/master` must be fetched again only after the assignment-specific exact-SHA gate is green, then merged into the feature branch before PR promotion per the SceneIssue workflow.

## Remaining gates

Run the corrected physical approach through one exact-head request on `ci-test/fixes/agent-2`. Require repository-derived module validation, the owned opening-control player, vitality regressions, and canonical Kentridge route through every System24 milestone. A cancelled/failed run cannot satisfy a checkbox. On terminal green, inspect exact built-player screenshots directly and require production-quality presentation. Then complete every supported task, close open -> closed with resolution metadata, fetch and merge latest master into the feature branch, promote only through PR + auto-merge, monitor required `affected`, and verify the closed SceneIssue on `origin/master`.