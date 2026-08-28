# Plan — Kentridge Kid-in-the-Well Quest + Inventory

## Observed / acceptance
`captures` is empty, so there are **0 marked regions**; acceptance is behavioral/architectural. Kentridge already generates a market well, but the live campaign lacked a playable well-quest/reward/inventory flow.

## Evidence / discriminators
- **Quest framework missing:** falsified. `QuestRuntime` already owns deterministic start/observe/step/completion; `CampaignRuntime` had parallel quest hash sets instead of composing it.
- **Well needs scene geometry:** falsified. `KentridgeTownPlanner` emits `KentridgeRole.Well` through the standard `SettlementPlan`.
- **Legacy behavior unknown:** falsified by `mounting-force/Code/TakeBoyHome.m`: Madeline/boy go to `kentridge-well`; the boy falls/disappears with falling/thud beats; Madeline returns.
- **First final candidate valid:** falsified by run `33207614927`: bootstrap session-locator API was not a stable scene boundary; zero-capture replay also lacked dimensions.
- **Repaired candidate compiled:** falsified by run `33211767651`: referencing `Game.Input` made unqualified `Input` in `Game.Kentridge.*` bind to that sibling namespace, breaking both old scene controls and the new panel before tests/player build.
- **Quest change caused Kentridge topology failure:** falsified. The authored `starting-pub` / `first-destination` world requirements are unchanged from `master`; the feature only adds the well quest story effect. Run `33212072412` passes the focused quest regression but the real player throws `WB3011` because `first-destination` is not reachable from `starting-pub`, leaving `SURFACE visible=0`.

## Selected fix
Use `QuestRuntime` as Campaign quest authority; author the two-step generated-well -> Madeline quest; keep reusable item ownership in `InventoryRuntime`; let the Kentridge session own exactly-once reward synchronization. Presentation resolves the generated well/session, renders inventory snapshots, and takes the existing `Ui` context lease. A namespace-local Unity-input bridge preserves the slice's existing direct exploration controls while `Game.Input` owns new menu context. Root 1600x900 dimensions make the architecture-only replay runnable.

## Required WB3011 startup repair
Trace `SettlementStreetTraversalFacts` and the generated Kentridge site projections for the canonical scene seed to identify why the standard `starting-pub -> first-destination` `TraversalProfile.NormalParty` requirement has no reachable candidate. Fix the topology/projection/planner layer that is producing the false/unrealizable reachability result; do **not** weaken or remove the campaign reachability constraint, suppress `WB3011`, bypass the production planner, or relax the player harness. Add a behavioral regression for the repaired generated-world reachability/startup invariant and verify the real Kentridge player renders nonzero world surface without startup exceptions.

## Regression / remaining gate
Focused PlayMode regression drives the authored quest through completion, synchronizes the production reward twice and asserts one item, then checks square inventory tile/context behavior. Add/retain a targeted regression proving the canonical Kentridge generated plan can resolve `starting-pub -> first-destination` for a normal party. Remaining gate: fresh exact-SHA PlayMode CI plus 30-second built Kentridge replay; inspect artifact for scene usability, nonzero rendered surface, no `WB3011`/startup exceptions, and quest/reward/inventory evidence.

## Blast radius / cost
Shared changes are quest event routing plus a tiny definition-backed inventory dictionary. Any WB3011 repair must stay at the narrow semantic traversal/projection boundary and preserve existing campaign constraints. No voxel storage/generation/rendering/streaming, equipment, persistence, crafting, journal, vendors, or second quest framework. Runtime cost is interaction-time quest scans, one idempotent reward lookup, and inventory IMGUI while present.
