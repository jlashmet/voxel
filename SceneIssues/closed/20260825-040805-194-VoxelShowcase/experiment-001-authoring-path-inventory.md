# Experiment 001 — authoring path inventory

## Hypothesis
Kentridge is authored through two public contracts: Game WorldBuilder owns semantic campaign planning, while the embedded MountingForce WorldGen package still owns and exposes the physical town plan directly to Game composition/showcase code.

## What was performed
Source commit: `ae37dbd2f9e5064f590f4a9af4b2521232f6e02e`.

Inspected the assigned capture, `CLAUDE.md`, the active world-feature-authoring spec, the embedded `Packages/com.mountingforce.worldgen` tree, `Assets/Game/WorldBuilder`, `Assets/Game/Composition/WorldBuilderWorldGen/Runtime/KentridgeCampaignWorldRealization.cs`, and `Assets/Game/Composition/Kentridge/Runtime/KentridgeCampaignSessionBootstrap.cs`.

## Result
Confirmed:
- the complete legacy Kentridge planner/architecture/voxel implementation remains a first-class embedded package at `Packages/com.mountingforce.worldgen`;
- Game already has a separate `Game.WorldBuilder.Api` / `Game.WorldBuilder.Runtime` subsystem;
- `KentridgeCampaignWorldPlanner` imports both `Game.WorldBuilder.Runtime` and `MountingForce.WorldGen*`, and its own comment states Composition invokes both runtimes;
- `KentridgeCampaignGenerationPlan` publicly exposes `MountingForce.WorldGen.SettlementPlan`;
- `KentridgeCampaignSessionBootstrap.Plan` publicly accepts that same legacy `SettlementPlan`.

That means a Game caller must already possess a MountingForce town plan before entering the Kentridge campaign bootstrap, so WorldBuilder is not the sole town-authoring API.

## What was learned
**Hypothesis confirmed.** The defect is a real API/ownership split, not just duplicate folder names. The migration must make the legacy generator a backend implementation detail owned inside the Game/Voxel Engine architecture and stop exposing its `SettlementPlan` as the Kentridge authoring entry point.

## Next
Define the smallest WorldBuilder-owned Kentridge/town authoring entry point and add a focused regression that prevents Game-facing Kentridge bootstrap/API code from exposing the legacy MountingForce planning contract.
