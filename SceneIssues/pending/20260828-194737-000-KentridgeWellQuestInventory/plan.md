# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed / acceptance
`captures` is empty, so there are **0 marked regions**; acceptance is behavioral/architectural. Kentridge already generates a market well, but the live campaign lacked a playable well-quest/reward/inventory flow.

## Evidence / discriminators
- **Quest framework missing:** falsified. `QuestRuntime` already owns deterministic start/observe/step/completion; `CampaignRuntime` had parallel quest hash sets instead of composing it.
- **Well needs scene geometry:** falsified. `KentridgeTownPlanner` emits `KentridgeRole.Well` through the standard `SettlementPlan`.
- **Legacy behavior unknown:** falsified by `mounting-force/Code/TakeBoyHome.m`: Madeline/boy go to `kentridge-well`; the boy falls/disappears with falling/thud beats; Madeline returns.
- **First final candidate valid:** falsified by exact run `33207614927`. The scene assembly rejected the new bootstrap session-locator members, and the built-player harness rejected this zero-capture issue because no screen dimensions were supplied.

## Selected fix
Use the existing `QuestRuntime` as Campaign quest authority and route semantic NPC/world observations into it. Author the two-step generated-well -> Madeline quest. Keep reusable item ownership in `InventoryRuntime`; Kentridge session/reward runtime owns the idempotent `Well Rescue Token` grant. Presentation only resolves the generated well, observes interaction, and renders inventory snapshots. It binds the already-created live slice session rather than exposing a bootstrap-global locator. Inventory takes the existing `Ui` input-context lease and bridges the slice's legacy direct input reader while open. Root 1600x900 dimensions make the architecture-only capture runnable by the standard player harness.

## Regression / remaining gate
Focused PlayMode regression drives the authored definition through `QuestRuntime`, verifies well -> Madeline completion, runs the production reward synchronizer twice and asserts one item, then checks `I`-viewer tile/context behavior. Remaining gate: fresh exact-SHA PlayMode CI plus 30-second built Kentridge replay with no startup/runtime exceptions.

## Blast radius / cost
Shared changes remain quest event routing plus a tiny definition-backed inventory dictionary. No voxel storage, generation, renderer, mesh, streaming, equipment, persistence, crafting, journal, vendor, or second quest framework. Runtime cost is interaction-time quest scans, one idempotent reward lookup, and inventory IMGUI only while the viewer exists.
