# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed / acceptance
`captures` is empty, so there are **0 marked regions** to inspect; acceptance is behavioral/architectural. Kentridge already generates its market well, but the live campaign had no playable well quest, completion reward, or inventory viewer.

## Evidence / hypotheses
- **Missing quest framework:** falsified. `QuestRuntime` already owns start/observe/step/completion; the fix composes it into `CampaignRuntime` instead of adding parallel quest state.
- **Missing well geometry:** falsified. generated Kentridge already emits `KentridgeRole.Well` through the standard settlement plan.
- **Legacy scenario uncertain:** falsified by `mounting-force/Code/TakeBoyHome.m`: Madeline and the boy approach `kentridge-well`; the boy disappears with falling/thud beats; Madeline returns. The recovered quest therefore uses well interaction followed by return-to-Madeline.
- **Feature broke Kentridge topology:** falsified by run `33212072412`: focused quest regression passed while the built scene failed earlier at `WB3011`, before usable surface publication. Current `master` independently repaired shared settlement traversal/intersection handling and added a production Kentridge scene regression; merging it removes the stale topology failure without weakening campaign constraints.
- Earlier candidates were rejected by run `33207614927` (unstable scene/session boundary and missing replay dimensions) and run `33211767651` (`Game.Input` namespace collision). The retained scene-local Unity input bridge resolves the latter.

## Selected fix
Use `QuestRuntime` as campaign quest authority; start the two-step generated-well -> Madeline quest from normal New Game story flow; route well/NPC gameplay observations into it; keep reusable item ownership/querying in `InventoryRuntime`; synchronize one unique reward from the Kentridge session. The thin scene presentation resolves the generated well and live session, opens/closes a read-only inventory under the existing `Ui` input context, and renders 64 px square item tiles. After the master scene-runtime reorganization, the presentation/bridge live under the canonical `Assets/Game/Composition/Kentridge/Playable/SceneRuntime` assembly.

## Regression / final gate
`KentridgeWellQuestInventoryTests.WellQuestProgressionGrantsExactlyOneVisibleInventoryItem` drives both quest steps, synchronizes completion twice, asserts exactly one reward, and checks inventory open/close, UI context, and square tile size. Master’s `WorldBuilderProductionScenePlayTests` covers the canonical Kentridge startup/surface invariant that previously raised `WB3011`. Final gate: fresh exact-SHA focused PlayMode CI plus the built Kentridge replay; require no startup exception, nonzero usable surface, and quest/inventory runtime evidence.

## Blast radius / cost
Shared changes are quest event routing and a definition-backed inventory dictionary; scene code is Kentridge-only. No second quest system, equipment, persistence, crafting, vendors, sorting/filtering, or voxel storage/rendering changes. Runtime cost is interaction-time quest scans, one idempotent reward lookup, and inventory IMGUI only while the presentation exists.
