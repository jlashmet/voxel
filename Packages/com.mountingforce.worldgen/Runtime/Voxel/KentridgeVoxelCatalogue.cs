using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Voxel backend for Kentridge. This is the only layer that knows both the semantic world model
    /// and VoxelEngine's feature VM. Rendering is deliberately not referenced.
    /// </summary>
    public static class KentridgeVoxelCatalogue
    {
        private const int DefinitionCount = 8;

        private sealed class CompiledProgram
        {
            public int[] Code;
            public int3 Door;
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            ArchitectureTheme theme = plan.Theme;
            int scale = settings.VoxelsPerDecimetre;

            var programs = new CompiledProgram[DefinitionCount];
            programs[(int)StructureArchetype.Townhouse] = HouseProgram(theme, settings, 74, 72, 2, false);
            programs[(int)StructureArchetype.WideHouse] = HouseProgram(theme, settings, 98, 82, 2, false);
            programs[(int)StructureArchetype.Shop] = HouseProgram(theme, settings, 92, 78, 2, true);
            programs[(int)StructureArchetype.Inn] = InnProgram(theme, settings);
            programs[(int)StructureArchetype.Warehouse] = WarehouseProgram(theme, settings);
            programs[(int)StructureArchetype.Mansion] = MansionProgram(theme, settings);
            programs[(int)StructureArchetype.Church] = ChurchProgram(theme, settings);
            programs[(int)StructureArchetype.Well] = WellProgram(theme, settings);

            int programLength = 0;
            for (int i = 0; i < programs.Length; i++) programLength += programs[i].Code.Length;

            var byArchetype = new List<PlannedSite>[DefinitionCount];
            for (int i = 0; i < DefinitionCount; i++) byArchetype[i] = new List<PlannedSite>();
            for (int i = 0; i < plan.Sites.Count; i++)
                byArchetype[(int)plan.Sites[i].Archetype].Add(plan.Sites[i]);

            var catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: DefinitionCount,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: plan.Sites.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int placementOffset = 0;

            for (int id = 0; id < DefinitionCount; id++)
            {
                var archetype = (StructureArchetype)id;
                CompiledProgram program = programs[id];
                for (int p = 0; p < program.Code.Length; p++)
                    catalogue.Program[programOffset + p] = program.Code[p];

                Int3 footprintDm = SettlementFootprints.For(plan, archetype);
                int3 footprint = new int3(
                    footprintDm.X * scale,
                    footprintDm.Y * scale,
                    footprintDm.Z * scale);
                catalogue.Anchors[id] = new AnchorSpec
                {
                    Name = archetype == StructureArchetype.Well ? "interaction" : "door",
                    LocalPosition = program.Door,
                    Facing = Facing.South,
                    SnapToGround = false,
                };

                catalogue.Definitions[id] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-" + archetype.ToString().ToLowerInvariant()),
                    Kind = FeatureKind.Structure,
                    BasePlane = BasePlaneRule.LowestGround,
                    Footprint = footprint,
                    MaxSlope = archetype == StructureArchetype.Well ? 2 : 3,
                    Precedence = archetype == StructureArchetype.Mansion ? 130 : 100,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = id,
                    AnchorCount = 1,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Code.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 160,
                };

                List<PlannedSite> sites = byArchetype[id];
                for (int i = 0; i < sites.Count; i++)
                    catalogue.ExplicitPlacements[placementOffset + i] =
                        ResolvePlacement(sites[i], footprint, seed, scale);

                catalogue.Rules[id] = new PlacementRule
                {
                    DefinitionId = id,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 3,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = placementOffset,
                    ExplicitCount = sites.Count,
                };

                programOffset += program.Code.Length;
                placementOffset += sites.Count;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException("Kentridge catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static ExplicitPlacement ResolvePlacement(PlannedSite site, int3 footprint,
                                                           uint seed, int scale)
        {
            int ox = site.PositionDm.X * scale;
            int oz = site.PositionDm.Y * scale;
            int lowest = int.MaxValue;
            int sampleStep = math.max(8, 16 * scale);

            for (int z = 0; z <= footprint.z; z += sampleStep)
            for (int x = 0; x <= footprint.x; x += sampleStep)
            {
                int h = TerrainQuery.HeightAt(ox + x, oz + z, seed);
                if (h < lowest) lowest = h;
            }

            return new ExplicitPlacement
            {
                // Terrain adaptation is a backend concern. Until the engine's automatic cut/fill
                // stage exists, sink the authored foundation slightly into the lowest sampled plot.
                Position = new int3(ox, lowest - 5 * scale, oz),
                Orientation = site.Orientation,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
        }

        private static CompiledProgram HouseProgram(ArchitectureTheme theme,
                                                     VoxelWorldGenSettings settings,
                                                     int widthDm, int depthDm,
                                                     int floors, bool shop)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();
            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(theme.Roof);
            byte cloth = settings.Materials.Resolve(MaterialRole.Cloth);

            int x0 = 12 * s;
            int z0 = 10 * s;
            int w = widthDm * s;
            int d = depthDm * s;
            int f = theme.FoundationHeightDm * s;
            int t = theme.WallThicknessDm * s;
            int floor = theme.FloorHeightDm * s;
            int wallHeight = floors * floor;
            int doorW = (shop ? 18 : 13) * s;
            int doorX = x0 + w / 2 - doorW / 2;
            int doorH = theme.DoorHeightDm * s;
            int roofH = theme.TypicalRoofHeightDm * s;
            int windowW = 12 * s;
            int windowH = theme.WindowHeightDm * s;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);
            int sideWindowZ = z0 + d / 2 - windowW / 2;

            b.Box(x0, 0, z0, w, f, d, foundation);
            b.Box(x0, f, z0, w, wallHeight, d, wall);
            b.Carve(x0 + t, f, z0 + t, w - 2 * t, wallHeight, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, doorH, t + s);

            // The street facade remains the most expressive face, but ordinary Kentridge
            // buildings are complete objects now: rear and side elevations get the same window
            // proportions and timber vocabulary instead of becoming blank shells when walked around.
            for (int storey = 0; storey < floors; storey++)
            {
                int wy = f + storey * floor + theme.WindowBaseDm * s;

                AddWindowZ(b, x0 + 12 * s, wy, z0, windowW, windowH, t + s, glass);
                AddWindowZ(b, x0 + w - 24 * s, wy, z0, windowW, windowH, t + s, glass);

                AddWindowZ(b, x0 + 12 * s, wy, rearZ, windowW, windowH, t + s, glass);
                AddWindowZ(b, x0 + w - 24 * s, wy, rearZ, windowW, windowH, t + s, glass);

                AddWindowX(b, x0, wy, sideWindowZ, t + s, windowH, windowW, glass);
                AddWindowX(b, sideRightX, wy, sideWindowZ, t + s, windowH, windowW, glass);
            }

            AddTimberFrame(b, x0, z0, w, d, f, wallHeight,
                           theme.BeamWidthDm * s, timber);

            if (shop)
            {
                int awningY = f + 27 * s;
                b.Box(x0 + 5 * s, awningY, z0 - 13 * s,
                      w - 10 * s, 3 * s, 15 * s, cloth);
                b.Box(x0 + 7 * s, f + 1 * s, z0 - 2 * s,
                      w - 14 * s, 3 * s, 8 * s, timber);

                // A small rear service opening makes shops read correctly from the alley side
                // without competing with the main storefront.
                int serviceW = 10 * s;
                int serviceX = x0 + w / 2 - serviceW / 2;
                b.Carve(serviceX, f, rearZ, serviceW, 20 * s, t + s);
            }

            b.Prism(x0 - theme.RoofOverhangDm * s,
                    f + wallHeight,
                    z0 - theme.RoofOverhangDm * s,
                    w + 2 * theme.RoofOverhangDm * s,
                    roofH,
                    d + 2 * theme.RoofOverhangDm * s,
                    PrismProfile.Gable, roof);

            int chimney = 9 * s;
            b.Box(x0 + w - 20 * s, f + wallHeight - 4 * s,
                  z0 + d - 22 * s, chimney, roofH + 18 * s, chimney, foundation);

            int3 door = new(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = door };
        }

        private static CompiledProgram InnProgram(ArchitectureTheme theme, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();
            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(theme.Roof);

            int x0 = 14 * s, z0 = 14 * s, w = 138 * s, d = 126 * s;
            int f = theme.FoundationHeightDm * s;
            int t = theme.WallThicknessDm * s;
            int wallH = theme.FloorHeightDm * 2 * s;
            int doorW = 20 * s;
            int doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);
            int windowH = theme.WindowHeightDm * s;

            b.Box(x0, 0, z0, w, f, d, foundation);
            b.Box(x0, f, z0, w, wallH, d, wall);
            b.Carve(x0 + t, f, z0 + t, w - 2 * t, wallH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, theme.DoorHeightDm * s, t + s);

            for (int storey = 0; storey < 2; storey++)
            {
                int y = f + storey * theme.FloorHeightDm * s + theme.WindowBaseDm * s;

                for (int bay = 0; bay < 4; bay++)
                {
                    int x = x0 + (16 + bay * 31) * s;
                    AddWindowZ(b, x, y, z0, 12 * s, windowH, t + s, glass);
                }

                for (int bay = 0; bay < 3; bay++)
                {
                    int x = x0 + (24 + bay * 42) * s;
                    AddWindowZ(b, x, y, rearZ, 12 * s, windowH, t + s, glass);
                }

                int sideA = z0 + 31 * s;
                int sideB = z0 + d - 43 * s;
                AddWindowX(b, x0, y, sideA, t + s, windowH, 12 * s, glass);
                AddWindowX(b, x0, y, sideB, t + s, windowH, 12 * s, glass);
                AddWindowX(b, sideRightX, y, sideA, t + s, windowH, 12 * s, glass);
                AddWindowX(b, sideRightX, y, sideB, t + s, windowH, 12 * s, glass);
            }

            AddTimberFrame(b, x0, z0, w, d, f, wallH, theme.BeamWidthDm * s, timber);
            b.Prism(x0 - 5 * s, f + wallH, z0 - 5 * s,
                    w + 10 * s, theme.GrandRoofHeightDm * s, d + 10 * s,
                    PrismProfile.Gable, roof);

            // Deep entrance porch and hanging-sign beam make the inn legible from the street.
            b.Box(doorX - 8 * s, f, z0 - 18 * s, doorW + 16 * s, 3 * s, 20 * s, foundation);
            b.Box(doorX - 7 * s, f, z0 - 15 * s, 4 * s, 30 * s, 4 * s, timber);
            b.Box(doorX + doorW + 3 * s, f, z0 - 15 * s, 4 * s, 30 * s, 4 * s, timber);
            b.Box(doorX - 7 * s, f + 27 * s, z0 - 15 * s, doorW + 14 * s, 4 * s, 4 * s, timber);

            // Back-of-house service entrance for kitchen/stable traffic.
            int serviceW = 13 * s;
            b.Carve(x0 + w / 2 - serviceW / 2, f, rearZ,
                    serviceW, theme.DoorHeightDm * s, t + s);

            int3 door = new(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = door };
        }

        private static CompiledProgram WarehouseProgram(ArchitectureTheme theme,
                                                         VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();
            byte stone = settings.Materials.Resolve(theme.Foundation);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);

            int x0 = 15 * s, z0 = 18 * s, w = 158 * s, d = 142 * s;
            int f = 8 * s, wallH = 55 * s, t = 5 * s;
            int doorW = 42 * s, doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);

            b.Box(x0, 0, z0, w, f, d, stone);
            b.Box(x0, f, z0, w, wallH, d, timber);
            b.Carve(x0 + t, f, z0 + t, w - 2 * t, wallH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, 38 * s, t + s);

            // Heavy external posts wrap front and rear so the building still reads as a working
            // warehouse when approached from the service lane.
            for (int i = 0; i <= 5; i++)
            {
                int x = x0 + math.min(w - 5 * s, i * 31 * s);
                b.Box(x, f, z0 - s, 5 * s, wallH, 4 * s, stone);
                b.Box(x, f, z0 + d - 3 * s, 5 * s, wallH, 4 * s, stone);
            }

            // High clerestory openings keep the long side walls industrial rather than domestic.
            int highY = f + 35 * s;
            for (int bay = 0; bay < 3; bay++)
            {
                int z = z0 + (26 + bay * 40) * s;
                AddWindowX(b, x0, highY, z, t + s, 10 * s, 16 * s, glass);
                AddWindowX(b, sideRightX, highY, z, t + s, 10 * s, 16 * s, glass);
            }

            // Rear cargo opening mirrors the functional front entrance without making the back
            // elevation identical to the street face.
            int rearDoorW = 34 * s;
            b.Carve(x0 + w / 2 - rearDoorW / 2, f, rearZ,
                    rearDoorW, 32 * s, t + s);

            b.Prism(x0 - 5 * s, f + wallH, z0 - 5 * s,
                    w + 10 * s, 32 * s, d + 10 * s, PrismProfile.Gable, roof);

            int3 door = new(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = door };
        }

        private static CompiledProgram MansionProgram(ArchitectureTheme theme,
                                                       VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();
            byte stone = settings.Materials.Resolve(theme.AccentStone);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte glass = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);

            int x0 = 26 * s, z0 = 26 * s, w = 210 * s, d = 188 * s;
            int f = 9 * s, t = 5 * s;
            int wallH = 3 * theme.FloorHeightDm * s;
            int doorW = 22 * s, doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);

            b.Box(x0, 0, z0, w, f, d, stone);
            b.Box(x0, f, z0, w, wallH, d, wall);
            b.Carve(x0 + t, f, z0 + t, w - 2 * t, wallH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, 30 * s, t + s);

            for (int storey = 0; storey < 3; storey++)
            {
                int y = f + storey * theme.FloorHeightDm * s + theme.WindowBaseDm * s;

                for (int bay = 0; bay < 5; bay++)
                {
                    int x = x0 + (18 + bay * 39) * s;
                    AddWindowZ(b, x, y, z0, 13 * s, theme.WindowHeightDm * s, t + s, glass);
                    AddWindowZ(b, x, y, rearZ, 13 * s, theme.WindowHeightDm * s, t + s, glass);
                }

                for (int bay = 0; bay < 3; bay++)
                {
                    int z = z0 + (38 + bay * 50) * s;
                    AddWindowX(b, x0, y, z, t + s,
                               theme.WindowHeightDm * s, 13 * s, glass);
                    AddWindowX(b, sideRightX, y, z, t + s,
                               theme.WindowHeightDm * s, 13 * s, glass);
                }

                if (storey > 0)
                {
                    int bandY = f + storey * theme.FloorHeightDm * s - 2 * s;
                    b.Box(x0, bandY, z0 - s, w, 4 * s, 3 * s, stone);
                    b.Box(x0, bandY, z0 + d - 2 * s, w, 4 * s, 3 * s, stone);
                }
            }

            // Stone corner quoins now wrap all four corners; only the formal portico remains
            // deliberately front-biased.
            b.Box(x0, f, z0 - s, 7 * s, wallH, 5 * s, stone);
            b.Box(x0 + w - 7 * s, f, z0 - s, 7 * s, wallH, 5 * s, stone);
            b.Box(x0, f, z0 + d - 4 * s, 7 * s, wallH, 5 * s, stone);
            b.Box(x0 + w - 7 * s, f, z0 + d - 4 * s, 7 * s, wallH, 5 * s, stone);
            b.Box(doorX - 18 * s, f, z0 - 24 * s, 7 * s, 42 * s, 7 * s, stone);
            b.Box(doorX + doorW + 11 * s, f, z0 - 24 * s, 7 * s, 42 * s, 7 * s, stone);
            b.Box(doorX - 20 * s, f + 38 * s, z0 - 26 * s,
                  doorW + 40 * s, 5 * s, 30 * s, timber);

            b.Prism(x0 - 6 * s, f + wallH, z0 - 6 * s,
                    w + 12 * s, theme.GrandRoofHeightDm * s, d + 12 * s,
                    PrismProfile.Gable, roof);

            int3 door = new(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = door };
        }

        private static CompiledProgram ChurchProgram(ArchitectureTheme theme,
                                                      VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();
            byte stone = settings.Materials.Resolve(theme.AccentStone);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte glass = settings.Materials.Resolve(theme.Window);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);

            int x0 = 22 * s, z0 = 18 * s, w = 120 * s, d = 132 * s;
            int f = 8 * s, t = 5 * s, naveH = 62 * s;
            int doorW = 20 * s, doorX = x0 + w / 2 - doorW / 2;
            int rearZ = z0 + d - (t + s);
            int sideRightX = x0 + w - (t + s);

            b.Box(x0, 0, z0, w, f, d, stone);
            b.Box(x0, f, z0, w, naveH, d, wall);
            b.Carve(x0 + t, f, z0 + t, w - 2 * t, naveH, d - 2 * t);
            b.Carve(doorX, f, z0, doorW, 30 * s, t + s);
            AddWindowZ(b, x0 + 16 * s, f + 27 * s, z0, 14 * s, 23 * s, t + s, glass);
            AddWindowZ(b, x0 + w - 30 * s, f + 27 * s, z0, 14 * s, 23 * s, t + s, glass);

            // Tall nave windows make the church recognisable from its long elevations, where the
            // previous prototype was almost completely blank.
            for (int bay = 0; bay < 3; bay++)
            {
                int z = z0 + (56 + bay * 29) * s;
                AddWindowX(b, x0, f + 25 * s, z, t + s, 25 * s, 12 * s, glass);
                AddWindowX(b, sideRightX, f + 25 * s, z, t + s, 25 * s, 12 * s, glass);
            }

            // Rear chancel windows provide an intentional termination to the nave.
            AddWindowZ(b, x0 + 30 * s, f + 30 * s, rearZ,
                       15 * s, 24 * s, t + s, glass);
            AddWindowZ(b, x0 + w - 45 * s, f + 30 * s, rearZ,
                       15 * s, 24 * s, t + s, glass);

            b.Prism(x0 - 5 * s, f + naveH, z0 - 5 * s,
                    w + 10 * s, 38 * s, d + 10 * s, PrismProfile.Gable, roof);

            // Front bell tower creates the vertical landmark visible over Kentridge's roofs.
            int towerW = 42 * s;
            int towerX = x0 + w / 2 - towerW / 2;
            b.Box(towerX, f, z0 + 5 * s, towerW, 112 * s, 42 * s, stone);
            b.Carve(towerX + 5 * s, f, z0 + 10 * s, towerW - 10 * s, 94 * s, 32 * s);
            b.Carve(doorX, f, z0, doorW, 30 * s, 12 * s);
            b.Prism(towerX - 4 * s, f + 112 * s, z0 + s,
                    towerW + 8 * s, 36 * s, 50 * s, PrismProfile.Gable, roof);

            int3 door = new(doorX + doorW / 2, f, z0);
            b.Anchor(0, door, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = door };
        }

        private static CompiledProgram WellProgram(ArchitectureTheme theme,
                                                    VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();
            byte stone = settings.Materials.Resolve(theme.AccentStone);
            byte timber = settings.Materials.Resolve(theme.Frame);
            byte roof = settings.Materials.Resolve(theme.Roof);
            byte water = settings.Materials.Resolve(MaterialRole.Water);
            int cx = 28 * s, cz = 28 * s;

            b.Cylinder(cx, 0, cz, 22 * s, 11 * s, 1, stone);
            b.Cylinder(cx, 3 * s, cz, 14 * s, 12 * s, 1, 0, PrimitiveMode.Carve);
            b.Cylinder(cx, 3 * s, cz, 13 * s, 2 * s, 1, water);
            b.Box(7 * s, 8 * s, 25 * s, 5 * s, 37 * s, 5 * s, timber);
            b.Box(44 * s, 8 * s, 25 * s, 5 * s, 37 * s, 5 * s, timber);
            b.Box(7 * s, 42 * s, 23 * s, 42 * s, 5 * s, 9 * s, timber);
            b.Prism(4 * s, 47 * s, 16 * s, 48 * s, 13 * s, 24 * s,
                    PrismProfile.Gable, roof);

            int3 interaction = new(cx, 11 * s, cz);
            b.Anchor(0, interaction, Facing.South);
            return new CompiledProgram { Code = b.Finish(), Door = interaction };
        }

        private static void AddWindowZ(ProgramBuilder b, int x, int y, int z,
                                       int width, int height, int depth, byte material)
        {
            b.Carve(x, y, z, width, height, depth);
            b.Box(x, y, z, width, height, depth, material);
        }

        private static void AddWindowX(ProgramBuilder b, int x, int y, int z,
                                       int depth, int height, int width, byte material)
        {
            b.Carve(x, y, z, depth, height, width);
            b.Box(x, y, z, depth, height, width, material);
        }

        private static void AddTimberFrame(ProgramBuilder b, int x0, int z0, int width, int depth,
                                           int baseY, int wallHeight, int beam, byte timber)
        {
            // Four structural corner posts.
            b.Box(x0, baseY, z0, beam, wallHeight, 2 * beam, timber);
            b.Box(x0 + width - beam, baseY, z0, beam, wallHeight, 2 * beam, timber);
            b.Box(x0, baseY, z0 + depth - 2 * beam, beam, wallHeight, 2 * beam, timber);
            b.Box(x0 + width - beam, baseY, z0 + depth - 2 * beam,
                  beam, wallHeight, 2 * beam, timber);

            // Horizontal bands wrap all four elevations at sill/mid/top levels. The front remains
            // the most detailed facade, but the timber language now survives a full walk around.
            int[] levels =
            {
                baseY,
                baseY + wallHeight / 2,
                baseY + wallHeight - beam,
            };

            for (int i = 0; i < levels.Length; i++)
            {
                int y = levels[i];
                b.Box(x0, y, z0, width, beam, 2 * beam, timber);
                b.Box(x0, y, z0 + depth - 2 * beam, width, beam, 2 * beam, timber);
                b.Box(x0, y, z0, 2 * beam, beam, depth, timber);
                b.Box(x0 + width - 2 * beam, y, z0, 2 * beam, beam, depth, timber);
            }
        }

        /// <summary>Small authoring helper for the engine's flat integer shape bytecode.</summary>
        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public void Prism(int x, int y, int z, int sx, int sy, int sz,
                              PrismProfile profile, byte material) =>
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                   (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);

            public void Cylinder(int cx, int y, int cz, int radius, int height,
                                 byte axis, byte material,
                                 PrimitiveMode mode = PrimitiveMode.Fill) =>
                Op(ShapeOp.EmitCylinder, cx, y, cz, radius, height, axis, material, 0, 0, (int)mode);

            public void Anchor(int index, int3 p, Facing facing) =>
                Op(ShapeOp.SetAnchor, index, p.x, p.y, p.z, (int)facing);

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _code.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(0); // all prototype operands are immediate integers
                _code.AddRange(operands);
            }
        }
    }
}