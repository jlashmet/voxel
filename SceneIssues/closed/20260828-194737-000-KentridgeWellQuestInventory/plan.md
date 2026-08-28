# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed / acceptance
`captures` is empty, so there are **0 marked regions** to inspect; acceptance is behavioral/architectural. Kentridge already generates its market well, but the live campaign had no playable well quest, completion reward, or inventory viewer.

## Evidence / hypotheses
- **Missing quest framework:** falsified. `QuestRuntime` already owns start/observe/step/completion; the fix composes it into `CampaignRuntime` instead of adding parallel quest state.
- **Missing well geometry:** falsified. Generated Kentridge already emits `KentridgeRole.Well` through the standard settlement plan.
- **Legacy scenario uncertain:** falsified by `mounting-force/Code/TakeBoyHome.m`: Madeline and the boy approach `kentridge-well`; the boy disappears with falling/thud beats; Madeline returns. The recovered quest therefore uses well interaction followed by return-to-Madeline.
- **Feature broke Kentridge topology:** falsified by run `33212072412`: the focused regression passed while the built scene failed earlier at `WB3011`. Master independently repaired shared traversal/intersection handling; that repair is now merged.
- **Final player is unhealthy:** falsified by run `33214363213` and its single infrastructure retry. Both passed the quest regression; the built player exited 0, logged `HARNESS done ... assertion failures 0`, stabilized at 122 visible chunks, and captured two frames. The sole failure was the wrapper requiring `SCENEISSUE camera pinned` although this issue has zero recorded poses.

## Selected fix
Use `QuestRuntime` as campaign quest authority; start the two-step generated-well -> Madeline quest from normal New Game story flow; route gameplay observations into it; keep reusable item ownership/querying in `InventoryRuntime`; synchronize one unique reward from the Kentridge session. The Kentridge-only presentation opens/closes a read-only inventory under the existing `Ui` context and renders 64 px square tiles. For scene validation, require camera-pin evidence only when `captures` contains a recorded pose; screenshot count, captured resolution, player exit, runtime assertions, and usable-scene evidence remain mandatory.

## Regression / final gate
`KentridgeWellQuestInventoryTests.WellQuestProgressionGrantsExactlyOneVisibleInventoryItem` drives both quest steps, synchronizes completion twice, asserts exactly one reward, and checks inventory open/close, UI context, and square tile size. Master’s `WorldBuilderProductionScenePlayTests` covers the canonical startup/surface invariant. Final gate: one fresh exact-SHA PlayMode request plus the built Kentridge replay.

## Blast radius / cost
Shared gameplay changes are quest event routing plus a definition-backed inventory dictionary; scene code is Kentridge-only. The harness change only skips an impossible camera-pin assertion for zero-capture issues and remains strict for visual captures. No equipment, persistence, crafting, vendors, sorting/filtering, or voxel rendering changes. Runtime cost is interaction-time quest scans, one idempotent reward lookup, and inventory IMGUI while present; replay overhead is one parsed integer.
