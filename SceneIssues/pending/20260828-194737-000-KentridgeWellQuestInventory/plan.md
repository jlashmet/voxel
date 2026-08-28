# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed / acceptance
`captures` is empty, so there are **0 marked regions**; acceptance is behavioral/architectural. Kentridge already generates a market well, but the live campaign lacked a playable well-quest/reward/inventory flow.

## Evidence / discriminators
- **Quest framework missing:** falsified. `QuestRuntime` already owns deterministic start/observe/step/completion; `CampaignRuntime` had parallel quest hash sets instead of composing it.
- **Well needs scene geometry:** falsified. `KentridgeTownPlanner` emits `KentridgeRole.Well` through the standard `SettlementPlan`.
- **Legacy behavior unknown:** falsified by `mounting-force/Code/TakeBoyHome.m`: Madeline/boy go to `kentridge-well`; the boy falls/disappears with falling/thud beats; Madeline returns.
- **First final candidate valid:** falsified by run `33207614927`: bootstrap session-locator API was not a stable scene boundary; zero-capture replay also lacked dimensions.
- **Repaired candidate compiled:** falsified by run `33211767651`: referencing `Game.Input` made unqualified `Input` in `Game.Kentridge.*` bind to that sibling namespace, breaking both old scene controls and the new panel before tests/player build.

## Selected fix
Use `QuestRuntime` as Campaign quest authority; author the two-step generated-well -> Madeline quest; keep reusable item ownership in `InventoryRuntime`; let the Kentridge session own exactly-once reward synchronization. Presentation resolves the generated well/session, renders inventory snapshots, and takes the existing `Ui` context lease. A namespace-local Unity-input bridge preserves the slice's existing direct exploration controls while `Game.Input` owns new menu context. Root 1600x900 dimensions make the architecture-only replay runnable.

## Regression / remaining gate
Focused PlayMode regression drives the authored quest through completion, synchronizes the production reward twice and asserts one item, then checks square inventory tile/context behavior. Remaining gate: fresh exact-SHA PlayMode CI plus 30-second built Kentridge replay; inspect artifact for scene usability, no startup/runtime exceptions, and quest/reward/inventory evidence.

## Blast radius / cost
Shared changes are quest event routing plus a tiny definition-backed inventory dictionary. No voxel storage/generation/rendering/streaming, equipment, persistence, crafting, journal, vendors, or second quest framework. Runtime cost is interaction-time quest scans, one idempotent reward lookup, and inventory IMGUI while present.
