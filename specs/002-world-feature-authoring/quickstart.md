# Quickstart: World Feature Authoring

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Worldbuilding plan**: [worldbuilding-plan.md](./worldbuilding-plan.md)

## The one idea

A region generates its slice of the world knowing only the seed, the catalogue, and its own
coordinates. Regions stream in any order, get evicted, and regenerate — so nothing may depend on a
neighbour, an accumulated structure, or a global pass.

Everything else in this design follows from that sentence. If a proposal needs to know what
another region did, it is wrong for this codebase, however natural it looks.

## How a castle gets into the world

```text
1. hash(seed, "castle", cellCoord)  ->  is there a castle in this cell? where? what parameters?
2. Region asks: which cells within a castle-footprint of me could reach into me?
3. For each, recompute step 1. Same hash, same answer, on any machine, in any order.
4. Evaluate the shape program with those parameters  ->  a list of boxes, cylinders, prisms.
5. Clip those primitives to this region's bounds. Rasterise into the brickmap.
```

Four regions each run this and each get their own quarter of the castle. They never talk. The
seams line up because both sides computed the same primitives from the same numbers.

## Why primitives instead of voxels

A shape program emits *primitives*, not voxels. That indirection buys three things:

- **Sub-volume evaluation is cheap.** Generating one region's sliver of a castle costs the
  primitives that overlap it, not the whole castle.
- **The far field gets something to draw.** Primitives rasterise at any resolution, so distant
  castles appear without materialising any voxels.
- **Validation can prove things.** Because control flow is bounded, the maximum primitive count
  and maximum extent are computable ahead of time rather than discovered at runtime.

Architectural vocabulary is deliberately one level above those primitives. A porch, buttress,
stair flight, apse, colonnade, gatehouse, dormer, or crypt is normally a bounded composition of
existing `Box`, `HollowBox`, `Cylinder`, `Prism`, `Arch`, opening, roof, and cardinal-transform
operations. Do not add an engine opcode merely because a designer needs a new named component.

## Presets: names, versions, and overrides

Preset factories are pure config constructors. They do not register themselves, mutate global
state, choose a seed, or directly author voxels. Stable IDs are separate metadata used by selection,
diagnostics, tooling, saves/configuration, and authored references.

The ID convention is:

```text
<archetype>.<variant>.v<positive-version>
```

Examples:

```text
house.compact-cabin.v1
house.farmhouse.v1
church.parish.v1
cathedral.gothic.v1
temple.classical-columned.v1
castle.walled.v1
cave.default.v1
```

Use lowercase ASCII letters/digits and kebab-case within the first two segments. Increment the
version when the *default meaning of the named preset* changes in a way callers may need to pin.
Changing an instance after calling the factory is just an override; it does not create another ID.

Engine-owned IDs live in `StructurePresetIds`; game-owned IDs live in `GameStructurePresetIds`.
`StructurePresetId.IsWellFormed(id)` validates the grammar. Core settlement planning deliberately
sees the IDs only as opaque strings so it never depends on game or voxel structure assemblies.

## Small house example

Start from a coherent preset, override only what this instance needs, validate it, then compile or
author it through the ordinary shared path.

```csharp
HouseConfig house = HouseStylePresets.CompactCabin(stoneMaterial, roofMaterial);
house.Roof.PitchRise = 3;
house.Roof.PitchRun = 4;

StructureDiagnostic diagnostic = StructureConfigDiagnostics.House(in house);
if (!diagnostic.IsValid)
    throw new InvalidOperationException(diagnostic.ToString());

StructureInspection preview = StructureInspectionTools.House(
    StructurePresetIds.HouseCompactCabinV1,
    in house);

int[] program = HouseProgramCompiler.BuildProgram(
    in house,
    mainDoorAnchorIndex: 0,
    hearthAnchorIndex: 1);
```

The `StructureInspection` is transient debug/authoring output: local bounds, facing, primary anchor,
summary, and validation status recomputed from the config. It is never persistent world truth.

## Detailed house example

Detailed houses still use `HouseConfig`; detail does not imply a private builder.

```csharp
HouseConfig farmhouse = HousePresetVariants.Farmhouse(masonryMaterial, timberMaterial);

// Per-instance authored override. The preset ID is still the base preset ID you chose to pin.
farmhouse.Dormers.Count = 2;
farmhouse.Dormers.Facade = HouseRoofFacade.Front;
farmhouse.Dormers.Width = 10;
farmhouse.Dormers.Height = 9;
farmhouse.Dormers.Depth = 8;
farmhouse.Dormers.Spacing = 18;
farmhouse.Dormers.EdgeMargin = 8;
farmhouse.Dormers.Style = RoofStyle.Gable;

StructureDiagnostic diagnostic = StructureConfigDiagnostics.House(in farmhouse);
```

Facade doors/windows, shutters, porch/exterior features, chimney, floor levels, roof and interior
hooks belong on the shared config graph. If a detail can be expressed by existing bounded operations,
add a config component + authoring composition rather than a house-style-specific geometry path.

## Castle example

Castle semantics live in the game layer while the component graph reuses shared walls, towers,
openings, roofs, battlements, floors, foundations, materials, and underground attachments.

```csharp
CastlePresetConfig castle = CastlePresets.WalledCastle(in plan, in palette);
StructureDiagnostic diagnostic = GameStructureConfigDiagnostics.Castle(in castle);
if (!diagnostic.IsValid)
    Debug.LogError(diagnostic);
```

Other current pure factories are `CastlePresets.Compatibility(...)` and
`CastlePresets.KeepOnly(...)`. Use `GameStructurePresetIds.CastleWalledV1` (or the matching ID) when
an authoring system needs a stable preset name.

## Cave example

Caves use a bounded local generation volume and an explicit entrance/attachment contract.

```csharp
CaveConfig cave = CaveConfig.Default;
CaveGenerationRequest request = CaveGenerationRequest.Standalone(
    seed: 0x5A17UL,
    terrainSeed: terrainSeed,
    surfaceAnchor: entranceWorldPosition,
    facing: Facing.North,
    width: 11,
    height: 13,
    clearanceLength: 18);

StructureDiagnostic diagnostic = StructureConfigDiagnostics.CaveRequest(in request, in cave);
StructureInspection preview = StructureInspectionTools.Cave(
    StructurePresetIds.CaveDefaultV1,
    in cave,
    in request);
```

`EnableLoops` remains rejected until there is a deterministic bounded reconnection contract. A cave
must not search arbitrary neighbouring/generated caves to decide whether a loop exists.

## Church and cathedral examples

```csharp
ChurchConfig parish = ChurchPresets.ParishChurch(in palette);
parish.EntryFacing = Facing.East;
StructureDiagnostic parishDiagnostic = GameStructureConfigDiagnostics.Church(in parish);
ChurchAuthoring.Author(session, origin, in parish);

CathedralWorldbuildingConfig gothic = CathedralWorldbuildingPresets.Gothic(in palette);
gothic.Cathedral.Church.EntryFacing = Facing.North;
StructureDiagnostic cathedralDiagnostic = GameStructureConfigDiagnostics.Cathedral(in gothic);
CathedralWorldbuildingAuthoring.Author(session, cathedralOrigin, in gothic);
```

Church/cathedral plans are authored in one local orientation and transformed cardinally. Buttresses,
including flying buttresses, are a shared component contract rather than cathedral-private geometry.
Crypt/cave attachment anchors rotate with the same structure transform.

## Temple example

```csharp
TempleConfig temple = TemplePresets.ClassicalColumned(in palette);
temple.EntryFacing = Facing.West;
StructureDiagnostic diagnostic = GameStructureConfigDiagnostics.Temple(in temple);
TempleAuthoring.Author(session, origin, in temple);
```

The temple composes shared stair, column, opening, roof, footprint, and palette contracts. The
courtyard variant is `TemplePresets.CourtyardTemple(in palette)`.

## Mixed city / settlement example

There is one production settlement planner for Kentridge; do **not** add a parallel city generator.
`SettlementPlan` owns stable role IDs, streets, plaza, plot positions, frontage and access. A
`SettlementCompositionPolicy` layers bounded lot rules, density/open-space policy, landmark rules,
and district-weighted reusable preset IDs over that plan.

```csharp
SettlementPlan town = KentridgeTownPlanner.Build(seed);
SettlementCompositionPolicy policy = KentridgeTownPlanner.CompositionPolicy;

SettlementPlacementInspection[] rows =
    SettlementCompositionInspection.Build(town, policy);

foreach (SettlementPlacementInspection row in rows)
    Debug.Log(row.ToString());
```

Generated house/shop/inn forms resolve a preset by stable role identity + seed + district, then the
voxel adapter passes the resulting `HouseConfig` through `HouseProgramCompiler`. Kentridge's authored
road coordinates, stable gameplay roles, frontage, and explicit placements remain unchanged.
Landmarks that do not yet have a shared shape-program compiler remain explicit archetypes rather than
forcing a second geometry conversion path.

A settlement policy is valid only when `PlanningScope == RegionLocal` and its candidate count,
planning span, lots, landmarks, and open spaces are finite. Global/unbounded city optimization is
rejected at policy validation.

## Adding a new structure component

Use this order. Most new worldbuilding vocabulary should stop before step 5.

1. **Define data in the API assembly.** Example: `ButtressConfig`, `ColumnConfig`, `StairConfig`, or a
   new `BalconyConfig`. Keep dimensions integer and add an `IsWellFormed`/diagnostic contract.
2. **Author it with existing spatial operations.** Put the deterministic bounded implementation in
   Runtime. Operate through `IStructureAuthoringSession`; do not mutate a global world model.
3. **Compose it into archetypes.** House/church/castle/etc. configs reference the shared component.
   The archetype owns semantic placement; the component owns only its local geometry contract.
4. **Expose a pure preset if useful.** A preset chooses config values. Give stable presets versioned
   IDs, but never make the ID a mutable registry lookup required for generation.
5. **Add an engine primitive/opcode only for genuinely new spatial semantics.** If boxes,
   cylinders, prisms, arches, hollow shells, carve/fill, transforms, or bounded repetition can express
   it, it is a composition problem, not an engine-primitive problem.

A component implementation must have an explicit finite maximum operation count or derive one from
bounded config fields. No data-dependent unbounded loops, no scans for neighbouring authored state,
and no random calls whose sequence depends on traversal order.

## Seed, local-coordinate, material, connectivity, and locality rules

### Seed

- Every procedural decision is a pure function of an explicit seed plus stable semantic identity.
- Hash stable candidate/role IDs; do not consume one shared random stream while iterating a list.
- Reordering candidates or streaming regions must not change a result.
- If a subsystem needs a sub-seed, derive it with a documented integer hash/salt.

### Local coordinates

- Author a structure in definition-local integer coordinates first.
- Apply one explicit origin/cardinal transform at the composition boundary.
- Keep config bounds local. World coordinates are derived, not stored as hidden persistent truth.
- Cardinal rotation must transform geometry *and* semantic anchors/frontage together.

### Material roles

- Configs carry semantic roles (`Foundation`, `PrimaryWall`, `Roof`, `Trim`, `Glass`, etc.) rather
  than application material bytes wherever possible.
- Resolve roles through the supplied palette at authoring/compilation time.
- A preset may choose roles; the application chooses actual material identities.

### Connectivity

- Doors, cave entrances, crypts, roads, plazas, and similar connections are explicit semantic
  anchors/access records. Do not infer connectivity by looking at already-generated voxels.
- Settlement plots keep explicit `PlannedSiteAccess` to a street/plaza target and a cardinal
  frontage. Architecture connects its public entrance to that authored access.
- Structure-attached caves/crypts receive an attachment anchor; they do not search for a parent.

### Region locality

- Candidate discovery and evaluation must be bounded from declared footprints/radii/spans.
- A region may recompute candidates that *could overlap it*; it may not ask another region what it
  generated.
- Density, spacing, landmark, and open-space policies require finite candidate and planning bounds.
- Reject a config that requires a global pass rather than silently approximating it with mutable
  state.

## Author-facing diagnostics

Use diagnostics before invoking a runtime authorer when content is editable or generated from tools.
The engine diagnostics currently cover preset IDs, houses, cave configs and cave requests; game
diagnostics cover shed, church, cathedral, temple and castle compositions.

```csharp
StructureDiagnostic diagnostic = GameStructureConfigDiagnostics.Temple(in temple);
if (!diagnostic.IsValid)
{
    // e.g. InvalidComposition at Colonnade: ...
    Debug.LogError($"{diagnostic.Code} {diagnostic.Field}: {diagnostic.Message}");
}
```

Diagnostics deliberately return the first actionable config path rather than maintaining a second
validation graph. Runtime `IsWellFormed` checks remain the authoritative cheap gate used by normal
generation.

## Where to look

| Question | File |
|---|---|
| What is being built and why | [spec.md](./spec.md) |
| Worldbuilding implementation phases | [worldbuilding-plan.md](./worldbuilding-plan.md) |
| Current worldbuilding inventory/seams | [worldbuilding-inventory.md](./worldbuilding-inventory.md) |
| Why the design is shaped this way | [research.md](./research.md) |
| What the data is | [data-model.md](./data-model.md) |
| What a designer authors | [contracts/catalogue-format.md](./contracts/catalogue-format.md) |
| What a shape program can do | [contracts/shape-program.md](./contracts/shape-program.md) |
| What the engine surfaces guarantee | [contracts/module-interfaces.md](./contracts/module-interfaces.md) |
| Numbers | `specs/001-destructible-voxel-engine/device-matrix.md` (authoritative) |

## Rules that will bite you

1. **No float anywhere in deterministic generation.** Not in catalogue decisions, placement, or the
   evaluator. Probabilities are integer thresholds/hashes. A float that reaches generation can become
   a cross-platform divergence that no single client can detect.
2. **Declared footprint is a promise, not a hint.** It bounds the neighbourhood scan for every
   region in the world. Content outside it is a validation failure.
3. **Features cannot see each other or the brickmap.** Contested space is settled by precedence.
   A program that inspects what is already there would depend on generation order.
4. **Order independence is the acceptance property.** Generate the same content in different
   traversal/streaming orders; the world must resolve identically.
5. **Shape is derived; only required ownership/protection is stored.** If you find yourself wanting
   to persist where deterministic structure geometry is, first ask whether it can be recomputed.
6. **Do not create a second planner to get new city visuals.** Extend the production settlement
   plan/composition/structure-realization seams instead.

## First run through the code

Milestone order in [plan.md](./plan.md) is also the reading order:

1. `ShapeProgram` + `PrimitiveRasteriser` — a definition stamped at a fixed coordinate.
2. `PlacementLattice` + `CandidateScan` — where things go, and the order-independence property.
3. `TerrainAdaptation` — meeting the ground without a step at region borders.
4. `Assets/VoxelEngine/Structures/Api` — reusable structure component/config contracts.
5. `Assets/Game/Structures/Api` + `Runtime` — game-owned archetype composition.
6. `Packages/com.mountingforce.worldgen/Runtime/Core` — semantic settlement plan/composition policy.
7. `Packages/com.mountingforce.worldgen/Runtime/Voxel` — production settlement-to-shape-program seam.

## Running anything in Unity

Use `tools/unity-run.sh`. Never invoke the Unity binary directly, and check whether an editor is
already open — the wrapper will refuse, and it refuses for a reason recorded in `CLAUDE.md`.

Note that batchmode play-mode tests do not exercise the editor lifecycle. Anything touching
`OnEnable`, domain reload, or GPU resource lifetime needs an EditMode test that loops the
lifecycle.
