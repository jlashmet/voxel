# WorldBuilder Runtime Integration

This document defines the runtime boundary between the WorldBuilder work and the gameplay/voxel subsystems that will consume it. It is intentionally narrower than the WorldBuilder implementation itself: the goal is to let Character, Edits, presentation, and scene/bootstrap work proceed independently without introducing duplicate runtimes or cross-system implementation references.

## Ownership

WorldBuilder owns semantic authoring and planning:

- site roles and spatial constraints;
- NPC semantic identity and target site;
- cutscene/site/actor bindings;
- story triggers, conditions, effects, and objectives;
- required/procedural secret intent and loot intent.

WorldGen owns generated physical facts:

- concrete settlement/site selection candidates;
- structure footprint, entrance, interior envelope, and traversal topology;
- hidden-space topology and false-wall geometry;
- exact terrain-relative world placement.

Cutscenes owns choreography and playback. Story owns runtime story-state evaluation.

`Game.Composition.*` is the only layer that joins these runtimes. Do not add WorldBuilder -> Character Runtime, WorldBuilder -> Edits Runtime, or WorldBuilder -> Voxel Runtime dependencies.

## Kentridge application flow

The current Kentridge integration is deliberately two-phase because story/site constraints must influence generation before exact terrain-relative coordinates exist.

```text
CampaignBlueprint
    |
    v
KentridgeCampaignSessionBootstrap.Plan(...)
    |
    +--> site-role resolution
    +--> NPC -> ResolvedSiteId assignments
    +--> physical hidden-space architecture
    +--> distinct secret selection
    |
    v
KentridgeCampaignGenerationPlan
    |
    +--> HiddenSpaces -> secret-aware Kentridge voxel catalogue
    |
    v
voxel/terrain generation + exact site placement
    |
    v
KentridgeCampaignSessionBootstrap.CreateSession(...)
    |
    +--> exact NPC world positions
    +--> exact cutscene stage bindings
    +--> exact hidden-room / false-wall / container geometry
    +--> authoritative gameplay adapters
    |
    v
CampaignRuntime
```

The generated hidden-space geometry used for gameplay selection is the same geometry passed into voxel emission. Do not run a second hidden-space planning pass in the voxel backend.

## Character runtime contract

`Game.Composition.Kentridge.Api.IKentridgeCampaignActorHost` is the current integration seam.

The eventual authoritative Character/player implementation should implement or adapt to it; WorldBuilder must not grow a second character runtime.

Required behavior:

- player slots already exist before session creation and resolve through `TryResolvePlayer`;
- `PrepareNpcs(...)` accepts the complete generated NPC placement batch;
- batch application is atomic within the Character subsystem;
- after the call returns, every supplied `NpcRef` resolves through `TryResolveNpc`;
- returned actors implement `ICutsceneActorRuntime` so setup/move/facing operations affect the authoritative actor.

The current `feature/character-factory-runtime-api` work exposes equipment/part APIs but does not yet expose authoritative spawn/pose/movement. When that API is added, adapt it here instead of copying Character implementation into Composition.

## Presentation contract

Cutscene presentation is client-local and never owns authoritative gameplay state.

`Game.Cutscenes.Api` exposes independent adapters:

- `ICutsceneCameraCueRuntime`;
- `ICutsceneDialogueCueRuntime`;
- `ICutsceneSoundCueRuntime`.

`Game.Cutscenes.Runtime.CutscenePresentationRouter` composes them into `ICutscenePresentation`.

Dialogue receives both the authored cue and the semantic speaker when one is explicit. A default speaker remains valid for recovered legacy cues where the cue itself owns speaker selection.

## Secret runtime contract

`Game.Composition.Kentridge.Api.IKentridgeCampaignSecretHost` receives the complete `ResolvedSecretWorldGeometry` batch before a secret-bearing campaign is considered gameplay-ready.

The secret host owns:

- interaction registration;
- the visible/hidden state of the false wall;
- routing destruction/removal through Edits;
- container creation at `ContainerFloorPoint`;
- reward/loot activation after the entrance is opened according to gameplay rules.

Composition supplies exact geometry and semantic identity; it does not issue legacy voxel mutations directly.

## Edits precision requirement

The Kentridge generated false wall is currently exactly:

```text
4 dm thick x 24 dm high x 8 dm wide
```

At the current Kentridge realization scale of 1 voxel/dm, that is a 4 x 24 x 8 voxel AABB.

The active architecture refactor's `VoxelEngine.Edits.Api.AlterationEvent.CreateCubeBrush(...)` expresses dimensions in `VoxelReadGrid` blocks, whose edge is 8 voxels. Expanding the 4-voxel wall thickness to one whole 8-voxel block would delete neighboring structure and is therefore not a valid integration.

The final Edits API must provide a deterministic, replicable exact-voxel/AABB removal operation (or an equivalent canonical operation with the same precision). `RawBatch` is currently reserved and is not a usable substitute.

Do not solve this by rounding the generated bounds in Composition.

## Session preflight and mutation order

`KentridgeCampaignSessionBootstrap.CreateSession(...)` performs non-mutating validation before gameplay-owned state is changed:

1. blueprint/generation-plan identity;
2. exact world realization;
3. required secret-host presence;
4. required player-slot availability;
5. NPC placement completeness/uniqueness;
6. secret batch preparation, if any;
7. NPC batch preparation;
8. verify all prepared NPCs resolve;
9. verify every cutscene actor binding resolves;
10. construct `CampaignRuntime`.

Each gameplay subsystem is responsible for applying its own supplied batch atomically. Composition does not pretend it can provide a distributed transaction across Character and Secret/Edits runtimes.

## Known opening integration

Production authoring lives in `Game.Composition.Campaign.Content.KnownOpeningCampaignContent`.

Known facts remain deliberately limited to recovered/established content:

- starting pub;
- Madeline, Steven, Logan;
- recovered Kentridge opening cutscene;
- a different reachable first-destination site selected by constraints;
- a semantic destination NPC at that generated site;
- travel objective and known story transitions.

The destination cutscene definition remains injected because its dialogue/choreography has not been recovered. Do not invent a destination archetype, NPC name, dialogue, or choreography to make the bootstrap more concrete.

## Integration rules

- Keep WorldBuilder coordinate-free.
- Keep WorldGen Core/Architecture independent of VoxelEngine.
- Keep ordinary Runtime assemblies from referencing foreign Runtime assemblies.
- Put cross-runtime application wiring in `Game.Composition.*` only.
- Prefer exact generated facts over archetype-derived assumptions.
- Fail closed when a requested capability cannot be physically realized.
- Do not merge the divergent Character/architecture branches wholesale into `worldbuilder`; port/adapt through stable APIs after their cutovers land.
