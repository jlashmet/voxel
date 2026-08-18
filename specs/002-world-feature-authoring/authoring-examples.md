# Worldbuilding Authoring Examples

This supplements [quickstart.md](./quickstart.md) with the two composition cases that are easiest to
get subtly wrong: structure-attached underground generation and semantic seed stability.

## Standalone cave

A standalone cave receives its own stable seed, terrain seed, explicit surface entrance, cardinal
facing, and finite local bounds from `CaveConfig`.

```csharp
CaveConfig cave = CaveConfig.Default;
CaveGenerationRequest standalone = CaveGenerationRequest.Standalone(
    seed: 0x5A17UL,
    terrainSeed: terrainSeed,
    surfaceAnchor: surfaceEntrance,
    facing: Facing.North,
    width: 11,
    height: 13,
    clearanceLength: 18);

StructureDiagnostic diagnostic =
    StructureConfigDiagnostics.CaveRequest(in standalone, in cave);
```

The request is sufficient to recompute the same cave; no previously generated cave object is an
input.

## Structure-attached cave

The parent structure owns a semantic `Cave` attachment anchor. The cave owns only its local bounded
generation rules. The connection is explicit rather than discovered by searching generated voxels.

```csharp
CathedralWorldbuildingConfig cathedral =
    CathedralWorldbuildingPresets.Gothic(in palette);

int3 caveAnchor = cathedral.Cathedral.ResolveCaveAnchor(cathedralOrigin);
Facing caveFacing = cathedral.Cathedral.ResolveCaveFacing();

CaveConfig cave = CaveConfig.Default;
CaveGenerationRequest attached = CaveGenerationRequest.Attached(
    seed: 0xCA7E5EEDUL,
    structureAnchor: caveAnchor,
    facing: caveFacing,
    width: 11,
    height: 13,
    clearanceLength: 18);

StructureDiagnostic diagnostic =
    StructureConfigDiagnostics.CaveRequest(in attached, in cave);
```

The same pattern is used by castle dungeon/cave attachments. Rotate the parent anchor with the parent
structure; do not rotate a generated cave afterward or infer the connection from occupancy.

## Semantic seed stability

Random-looking structure choices are keyed by **stable semantic identity**, not by consuming a
shared sequential random stream. Editing a chimney, window, roof, or other local config must not
reshuffle unrelated lots in a settlement.

Kentridge's weighted preset selection is a pure function of settlement seed + stable role ID +
archetype + district:

```csharp
SettlementPlan town = KentridgeTownPlanner.Build(seed);
BuildingPlot plot = town.Plots[7];

string selectedBefore = KentridgeTownPlanner.CompositionPolicy.Palette.SelectPreset(
    seed,
    plot.RoleId,
    plot.Archetype,
    plot.District);

// Local authored detail changes after preset selection.
HouseConfig house = HouseStylePresets.Farmhouse(masonry, timber);
house.Chimney.Enabled = !house.Chimney.Enabled;
house.FrontWindows.Count = 2;

// No random-stream state exists for the edit to advance. Re-resolving the same semantic candidate
// therefore produces the same structure choice.
string selectedAfter = KentridgeTownPlanner.CompositionPolicy.Palette.SelectPreset(
    seed,
    plot.RoleId,
    plot.Archetype,
    plot.District);

Debug.Assert(selectedBefore == selectedAfter);
```

The stronger rule is broader than this example: adding/removing/reordering another candidate must
not change a candidate's result either. Give each decision a stable ID/salt and hash it directly.

## Inspection: what tooling may display

`StructureInspection` is an ephemeral recomputation intended for inspector/debug UI. For a compiled
house it reports:

- stable preset ID,
- resolved archetype summary,
- local minimum + size (the footprint/bounds),
- cardinal facing,
- primary `door` and secondary `hearth` anchors,
- exact primitive count emitted by the current shared house compiler contract,
- the first author-facing validation failure, if any.

```csharp
StructureInspection inspection = StructureInspectionTools.House(
    StructurePresetIds.HouseFarmhouseV1,
    in house);
Debug.Log(inspection.ToString());
```

For seed-dependent generators such as caves, config-only inspection marks primitive count unresolved.
A preview evaluator that actually evaluates the seed may attach its measured count transiently:

```csharp
StructureInspection caveInspection = StructureInspectionTools.Cave(
    StructurePresetIds.CaveDefaultV1,
    in cave,
    in attached);

caveInspection = caveInspection.WithPrimitiveCount(evaluatedPrimitiveCount);
```

That decorated value is debug output only. Do not persist it as authoritative structure state.

For settlements, `SettlementCompositionInspection.Build(plan, policy)` recomputes each row's chosen
preset/source, resolved plot position, district, frontage, and explicit street/plaza access. It does
not own a second plan or catalogue.
