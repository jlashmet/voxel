# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed / marked regions
`captures` is empty, so there are **0 marked regions** to inspect; acceptance is behavioral/architectural. Kentridge has a generated market well but no playable quest/reward/inventory flow.

## Runtime evidence / competing hypotheses
- **Quest runtime already wired:** falsified. `CampaignRuntime` explicitly keeps `_activeQuests/_completedQuests` because `QuestRuntime` is unwired; production NPC interaction never sends a `QuestObservation`.
- **Quest framework missing:** falsified. `Game.Quests.Runtime.QuestRuntime` already owns deterministic start/observe/step/completion state and only needs authored definitions + production observations.
- **Well needs bespoke scene geometry:** falsified. `KentridgeTownPlanner` already emits `KentridgeRole.Well` in the market plaza through the standard `SettlementPlan`.
- **Legacy behavior unknown:** falsified by `mounting-force/Code/TakeBoyHome.m`: Madeline and the boy walk to `kentridge-well`; the boy falls/disappears with falling/thud beats; Madeline returns to the player.

## Selected fix
Wire `CampaignRuntime` to the existing `QuestRuntime`, route semantic NPC/world interactions into it, and author a two-step Kentridge quest: rescue/interact at the generated well, then return to Madeline. Start it through the existing Story quest seam. Add a reusable definition-backed inventory runtime; Kentridge grants one `Well Rescue Token` idempotently on completion. A small Kentridge presentation host resolves the generated well, supplies the interaction prompt/dialogue beats, and opens a read-only square-tile inventory with `I`.

## Regression / repro
Focused PlayMode regression will run the authored definition through the real quest runtime, advance well -> Madeline, assert completion, deliver the reward twice through the idempotent inventory boundary, and assert one visible inventory snapshot item. Final targeted CI also replays `KentridgePlayableSlice` for 30 seconds so the built-player scene startup/runtime path is captured.

## Blast radius / cost
Shared changes are limited to quest composition/event routing and the new inventory module. No per-voxel storage, generation, rendering, mesh, or streaming work is added. Gameplay cost is one small quest observation scan on interactions plus one tiny inventory dictionary; UI draws only while inventory is open. No equipment, persistence, crafting, journal, sorting, vendors, or second quest framework.
