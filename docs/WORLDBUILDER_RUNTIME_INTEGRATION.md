# WorldBuilder Runtime Integration

This document defines the runtime boundary between the WorldBuilder work and the gameplay/voxel subsystems that will consume it. It is intentionally narrower than the WorldBuilder implementation itself: the goal is to let Character, Edits, presentation, and scene/bootstrap work proceed independently without introducing duplicate runtimes or cross-system implementation references.

## Ownership

WorldBuilder owns semantic authoring and planning:

- region, route, settlement, and site ownership hierarchy;
- route-access and connector-length requirements;
- site roles and spatial constraints;
- NPC semantic identity and target site;
- cutscene/site/actor bindings;
- story triggers, conditions, effects, and objectives;
- required/procedural secret intent and loot intent.

WorldGen owns generated physical facts:

- concrete region/settlement/site geometry and selection candidates;
- structure footprint, entrance, interior envelope, and traversal topology;
- hidden-space topology and false-wall geometry;
- exact terrain-relative world placement.

Cutscenes owns choreography and playback. Story owns runtime story-state evaluation.

`Game.Composition.*` is the only layer that joins these runtimes. Do not add WorldBuilder -> Character Runtime, WorldBuilder -> Edits Runtime, or WorldBuilder -> Voxel Runtime dependencies.

## Backward-derived site requirements

Authored story/cutscene content should drive generation whenever the requirement is mechanically implied. `CampaignBlueprint` normalizes those implications into `SiteCapabilityRequirement` values with `SiteCapabilitySource.Derived` provenance.

Current backward derivation includes:

- any staged cutscene -> `CutsceneStage`;
- `SiteInterior` or `InteriorGatheringArea` stage region -> `Interior`;
- `PublicEntrance` or `EntranceApproach` stage region -> `PublicExit`;
- `PlayerSpawnArea` stage region -> minimum `PlayerSpawn(1)`;
- NPC requiring conversation -> `ConversationSpace`;
- hard required secret -> `SecretCandidateHost`.

An explicit authored capability of the same kind wins and preserves its authored capacity/provenance. For example, the known opening cutscene only proves that at least one player spawn exists, while the game requirement that the starting pub supports four players remains explicitly authored as `PlayerSpawn(4)`.

Do not duplicate derived topology requirements in campaign content just to make a current backend pass. If a backend cannot satisfy a derived requirement, the backend/candidate facts are incomplete.

## Compiled hierarchy contract

`CampaignBlueprint.Hierarchy` is authored semantic intent. `BlueprintCompiler` converts it into `PlanningGraph.HierarchyPlan`, the generator-facing hierarchy snapshot.

`WorldHierarchyPlan` groups the information a generator actually consumes:

- each region's biome, routes, settlements, and directly region-owned sites;
- each route's region, kind, importance, and settlement-access requirements;
- each settlement's region, archetype, population range, route access, and owned sites;
- exact route-to-settlement connector-length ranges;
- explicit region-vs-settlement site ownership.

Downstream generation must consume `HierarchyPlan` rather than re-parsing authoring objects or recovering semantics from dependency-node strings. The dependency graph still controls ordering; the typed hierarchy plan carries physical-generation requirements.

### Hierarchy cutover checklist

- [x] Compile authored region/route/settlement/site ownership into `PlanningGraph.HierarchyPlan`.
- [x] Derive Kentridge's semantic region/settlement owner from the compiled hierarchy plan.
- [x] Fail closed when Kentridge cannot prove an authored hierarchy requirement.
- [x] Filter generated site candidates from compiled `WorldSitePlacementPlan` ownership instead of re-scanning raw `WorldHierarchyBlueprint` authoring.
- [ ] Add typed settlement-plan selection when a multi-settlement WorldGen backend exists.
- [ ] Remove Kentridge-specific hierarchy gates incrementally as WorldGen exposes biome, outer-route, population, and connector realization facts.

## Kentridge application flow

The current Kentridge integration is deliberately two-phase because story/site constraints must influence generation before exact terrain-relative coordinates exist.

```text
CampaignBlueprint
    |
    v
BlueprintCompiler
    |
    +--> PlanningGraph.HierarchyPlan
    +--> site-role / NPC / cutscene / secret plans
    |
    v
KentridgeCampaignSessionBootstrap.Plan(blueprint, settlement)
    |
    +--> derive the authored Kentridge region/settlement owner from HierarchyPlan
    +--> reject hierarchy requirements the current Kentridge backend cannot prove
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

The current Kentridge composition is explicitly a single-region, single-settlement planner. It requires exactly one authored settlement in `HierarchyPlan` and derives that settlement's region from the same plan. Do not pass duplicate `RegionRef`/`SettlementRef` values through bootstrap configuration. A future multi-settlement composition should select a typed settlement plan explicitly rather than reintroducing string/ID duplication.

Kentridge's existing `MountingForce.WorldGen.SettlementPlan` does not expose enough semantic facts to prove every `WorldHierarchyPlan` requirement. The Composition adapter therefore fails closed rather than silently dropping unsupported constraints. Today it accepts the known opening's one unspecified-biome region and Town settlement, but rejects:

- more than one region or settlement;
- a specific biome requirement;
- any outer WorldBuilder route;
- a settlement archetype other than `Town`/unspecified;
- any population range;
- any settlement-to-route connector requirement.

Those are not rejected because WorldBuilder cannot model them; they are rejected because the current Kentridge backend cannot prove it realized them. A future hierarchy-aware WorldGen implementation should consume the same typed plan and remove these Kentridge-specific gates as it gains those capabilities.

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

- `kentridge-region` contains the known Kentridge starting settlement;
- `kentridge` owns the starting pub;
- starting pub, with explicit four-player spawn capacity;
- Madeline, Steven, Logan;
- recovered Kentridge opening cutscene, which derives the pub's interior/public-access/staging requirements;
- a different reachable first-destination site owned only by the surrounding region and selected by constraints;
- a semantic destination NPC at that generated site, which derives conversation-space requirement;
- travel objective and known story transitions.

The first destination remains `ConstraintMatch` with `SiteArchetype.Unspecified`; region ownership does not invent its type. The destination cutscene definition remains injected because its dialogue/choreography has not been recovered. Do not invent a destination archetype, NPC name, dialogue, or choreography to make the bootstrap more concrete.

### Playable opening acceptance

The first player-facing Kentridge slice is now mechanically complete and Unity-validated. The acceptance path uses the production campaign/session composition, generated Kentridge voxel catalogue, and real `CharacterMotor`; the pub and town are one continuous generated voxel world rather than separate interior/exterior scenes or a portal transition.

- [x] New Game resolves `starting-pub` to the generated Kentridge Pub and places the lead player at the realized opening stage.
- [x] The authored opening cutscene starts and owns player control until completion.
- [x] Story progression advances and gameplay control returns after the cutscene.
- [x] `KentridgeGameplaySiteAccessResolver` derives entrance/interior/exterior approach facts from the same generated site placement contract used by voxel realization.
- [x] The generated Pub keeps a full player-sized public-door corridor clear after facade framing and through the exterior gameplay approach.
- [x] The real `CharacterMotor` can move from the interior approach, through voxel collision at the generated doorway, to the exterior approach.
- [x] `KentridgePlayableSlice` starts in the Pub, runs the intro, returns control, and physically exits into generated Kentridge town.
- [x] `KentridgePlayableSlice.unity` is the configured player-build launch scene.
- [x] The dedicated `Kentridge Playable Slice` CI gate proves the doorway-ordering regression plus both physical PlayMode acceptances; run `31931502976` passed on commit `23eb3b285c862e72055e042ace0c016f70c17021`.

Presentation polish is deliberately not part of this mechanical acceptance gate. The thin slice still uses placeholder NPC runtime visuals and minimal camera/dialogue/sound cue presentation. Those should be replaced by the authoritative Character and presentation adapters as their APIs become available; do not duplicate those runtimes inside this scene driver.

## Integration rules

- Keep WorldBuilder coordinate-free.
- Keep WorldGen Core/Architecture independent of VoxelEngine.
- Keep ordinary Runtime assemblies from referencing foreign Runtime assemblies.
- Put cross-runtime application wiring in `Game.Composition.*` only.
- Let content-derived requirements flow backward instead of duplicating them in authoring.
- Consume compiled typed plans instead of reconstructing authoring semantics downstream.
- Prefer exact generated facts over archetype-derived assumptions.
- Fail closed when a requested capability cannot be physically realized or proven by the selected backend.
- Do not merge the divergent Character/architecture branches wholesale into `worldbuilder`; port/adapt through stable APIs after their cutovers land.
