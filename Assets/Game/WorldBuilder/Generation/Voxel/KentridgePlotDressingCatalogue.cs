using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Furnishes the private and semi-private space around Kentridge buildings from semantic plot
    /// information. Nothing in this pass knows about a camera, renderer, prefab, or showcase scene.
    /// A residential plot receives a small fenced garden; market buildings get a street sign and
    /// service clutter; the warehouse gets a cargo yard; the church gains a compact graveyard; and
    /// the noble estate receives a more formal hedge/fence treatment.
    ///
    /// Authoring happens in the same canonical local frame as buildings (front = south). Placement
    /// rectangles are then rotated by the plot frontage, so the dressing follows the town planner
    /// when a building moves or changes street side instead of preserving another coordinate map.
    /// Dressing also reproduces the plot terrace profile when choosing its Y coordinate, so fences
    /// on the raised parcel edge and props on the flat core both sit on the surface they actually see.
    /// </summary>
    public static class KentridgePlotDressingCatalogue
    {
        private enum DressingKind : byte
        {
            FenceX = 0,
            FenceZ = 1,
            GardenBed = 2,
            Signpost = 3,
            CargoStack = 4,
            GraveMarker = 5,
            HedgeCluster = 6,
        }

        private const int DefinitionCount = 7;
        private const int FenceLengthDm = 32;
        private const int FenceDepthDm = 4;
        // Must match KentridgePlotSurfaceCatalogue: the same 1.2 m feather is split into
        // voxel-scale steps so dressing sits on the softened plot transition.
        private const int TerraceStepDm = 1;
        private const int TerraceCount = 12;

        private readonly struct DressingPlacement
        {
            public readonly DressingKind Kind;
            public readonly int Xdm;
            public readonly int Zdm;
            public readonly int SurfaceY;

            public DressingPlacement(DressingKind kind, int xDm, int zDm, int surfaceY)
            {
                Kind = kind;
                Xdm = xDm;
                Zdm = zDm;
                SurfaceY = surfaceY;
            }
        }

        private readonly struct RectDm
        {
            public readonly int X;
            public readonly int Z;
            public readonly int Width;
            public readonly int Depth;

            public RectDm(int x, int z, int width, int depth)
            {
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            List<DressingPlacement> placements = BuildPlacements(plan, seed, scale);

            int[][] programs =
            {
                FenceXProgram(settings),
                FenceZProgram(settings),
                GardenProgram(settings),
                SignpostProgram(settings),
                CargoProgram(settings),
                GraveProgram(settings),
                HedgeProgram(settings),
            };

            int programLength = 0;
            for (int i = 0; i < programs.Length; i++) programLength += programs[i].Length;

            var byKind = new List<DressingPlacement>[DefinitionCount];
            for (int i = 0; i < DefinitionCount; i++) byKind[i] = new List<DressingPlacement>();
            for (int i = 0; i < placements.Count; i++)
                byKind[(int)placements[i].Kind].Add(placements[i]);

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: placements.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int placementOffset = 0;

            for (int id = 0; id < DefinitionCount; id++)
            {
                DressingKind kind = (DressingKind)id;
                int[] program = programs[id];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[id] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-plot-dressing-" + KindName(kind)),
                    // Decoration does not yet have its own engine FeatureKind. Landform keeps these
                    // props out of the semantic structure count while still using the feature VM.
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = Footprint(kind, scale),
                    MaxSlope = 32,
                    // Plot grading is 40, frontage paths 60, structures 100+. Dressing belongs in
                    // between: it rests on prepared plots while buildings remain authoritative.
                    Precedence = 80,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = 0,
                    AnchorCount = 0,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 12,
                };

                List<DressingPlacement> kindPlacements = byKind[id];
                for (int i = 0; i < kindPlacements.Count; i++)
                {
                    DressingPlacement placement = kindPlacements[i];
                    catalogue.ExplicitPlacements[placementOffset + i] = new ExplicitPlacement
                    {
                        Position = new int3(
                            placement.Xdm * scale,
                            placement.SurfaceY,
                            placement.Zdm * scale),
                        Orientation = 0,
                        OverrideOffset = 0,
                        OverrideCount = 0,
                    };
                }

                catalogue.Rules[id] = new PlacementRule
                {
                    DefinitionId = id,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 32,
                    MinSpacing = 0,
                    ClusterMin = 0,
                    ClusterMax = 0,
                    ExclusionMask = 0,
                    ExplicitOffset = placementOffset,
                    ExplicitCount = kindPlacements.Count,
                };

                placementOffset += kindPlacements.Count;
                programOffset += program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge plot dressing catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static List<DressingPlacement> BuildPlacements(
            SettlementPlan plan, uint seed, int scale)
        {
            var result = new List<DressingPlacement>(64);

            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;

                Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
                int baseSurfaceY = PlotSurfaceY(plan, plot, seed, scale) + scale;

                switch (plot.District)
                {
                    case DistrictKind.Residential:
                        AddResidential(result, plot, footprint, baseSurfaceY, scale);
                        break;
                    case DistrictKind.Market:
                        AddMarket(result, plot, footprint, baseSurfaceY, scale);
                        break;
                    case DistrictKind.Working:
                        AddWorking(result, plot, footprint, baseSurfaceY, scale);
                        break;
                    case DistrictKind.Noble:
                        AddNoble(result, plot, footprint, baseSurfaceY, scale);
                        break;
                    case DistrictKind.Civic:
                        AddCivic(result, plot, footprint, baseSurfaceY, scale);
                        break;
                }
            }

            // Scaled to the settlement being dressed rather than fixed at Kentridge's size. The
            // guard exists to catch a dressing pass that silently produced nothing; a smaller town
            // legitimately produces fewer placements, and a constant floor turned that into a
            // crash the moment a second settlement was generated.
            int minimumPlacements = Math.Max(8, plan.Plots.Count * 2);
            if (result.Count < minimumPlacements)
                throw new InvalidOperationException(
                    "Plot dressing for '" + plan.Id + "' produced implausibly few placements: " +
                    result.Count + " for " + plan.Plots.Count + " plots.");

            return result;
        }

        private static void AddResidential(List<DressingPlacement> result, BuildingPlot plot,
                                           Int3 footprint, int baseSurfaceY, int scale)
        {
            RectDm core = CorePadFor(plot.Archetype);

            AddFence(result, plot, footprint, 8, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);
            AddFence(result, plot, footprint,
                     footprint.X - 8 - FenceLengthDm, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);

            if ((KentridgeRole)plot.RoleId == KentridgeRole.AbandonedHouse)
            {
                AddSquare(result, plot, footprint, DressingKind.CargoStack,
                          core.X + 2, core.Z + core.Depth - 15, 14, baseSurfaceY, scale);
                return;
            }

            int gardenX = (plot.RoleId & 1) == 0
                ? core.X + 2
                : core.X + core.Width - 10;
            AddSquare(result, plot, footprint, DressingKind.GardenBed,
                      gardenX, core.Z + core.Depth - 9, 8, baseSurfaceY, scale);
        }

        private static void AddMarket(List<DressingPlacement> result, BuildingPlot plot,
                                      Int3 footprint, int baseSurfaceY, int scale)
        {
            RectDm core = CorePadFor(plot.Archetype);

            AddSquare(result, plot, footprint, DressingKind.Signpost,
                      core.X + core.Width - 9, core.Z + 1, 8, baseSurfaceY, scale);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      core.X + 1, core.Z + core.Depth - 15, 14, baseSurfaceY, scale);

            if (plot.Archetype == StructureArchetype.Inn)
                AddSquare(result, plot, footprint, DressingKind.CargoStack,
                          core.X + core.Width - 15, core.Z + core.Depth - 15,
                          14, baseSurfaceY, scale);
        }

        private static void AddWorking(List<DressingPlacement> result, BuildingPlot plot,
                                       Int3 footprint, int baseSurfaceY, int scale)
        {
            RectDm core = CorePadFor(plot.Archetype);
            int z = core.Z + core.Depth - 15;

            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      core.X + 2, z, 14, baseSurfaceY, scale);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      core.X + 24, z, 14, baseSurfaceY, scale);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      core.X + core.Width - 38, z, 14, baseSurfaceY, scale);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      core.X + core.Width - 16, z, 14, baseSurfaceY, scale);

            AddFence(result, plot, footprint, 5, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);
            AddFence(result, plot, footprint,
                     footprint.X - 5 - FenceLengthDm, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);
        }

        private static void AddNoble(List<DressingPlacement> result, BuildingPlot plot,
                                     Int3 footprint, int baseSurfaceY, int scale)
        {
            RectDm core = CorePadFor(plot.Archetype);
            int[] fenceX = { 24, 82, 140, 198 };
            for (int i = 0; i < fenceX.Length; i++)
                AddFence(result, plot, footprint, fenceX[i], footprint.Z - FenceDepthDm,
                         FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);

            int[] hedgeX =
            {
                core.X + 18,
                core.X + core.Width / 2 - 8,
                core.X + core.Width - 34,
            };
            for (int i = 0; i < hedgeX.Length; i++)
                AddSquare(result, plot, footprint, DressingKind.HedgeCluster,
                          hedgeX[i], core.Z + core.Depth - 17, 16, baseSurfaceY, scale);
        }

        private static void AddCivic(List<DressingPlacement> result, BuildingPlot plot,
                                     Int3 footprint, int baseSurfaceY, int scale)
        {
            RectDm core = CorePadFor(plot.Archetype);

            if (plot.Archetype == StructureArchetype.Church)
            {
                int[] graveZ = { 48, 72, 96, 120 };
                for (int i = 0; i < graveZ.Length; i++)
                    AddSquare(result, plot, footprint, DressingKind.GraveMarker,
                              core.X + 1, graveZ[i], 8, baseSurfaceY, scale);

                AddFence(result, plot, footprint, 2, 38,
                         FenceDepthDm, FenceLengthDm, baseSurfaceY, scale);
                AddFence(result, plot, footprint, 2, 104,
                         FenceDepthDm, FenceLengthDm, baseSurfaceY, scale);
                return;
            }

            AddFence(result, plot, footprint, 8, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);
            AddFence(result, plot, footprint,
                     footprint.X - 8 - FenceLengthDm, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, baseSurfaceY, scale);
            AddSquare(result, plot, footprint, DressingKind.GardenBed,
                      core.X + 2, core.Z + core.Depth - 9, 8, baseSurfaceY, scale);
            AddSquare(result, plot, footprint, DressingKind.GardenBed,
                      core.X + core.Width - 10, core.Z + core.Depth - 9,
                      8, baseSurfaceY, scale);
        }

        private static void AddFence(List<DressingPlacement> result, BuildingPlot plot,
                                     Int3 footprint, int localX, int localZ,
                                     int width, int depth, int baseSurfaceY, int scale)
        {
            RectDm world = RotateRect(plot, footprint, localX, localZ, width, depth);
            DressingKind kind = world.Width >= world.Depth
                ? DressingKind.FenceX
                : DressingKind.FenceZ;
            int surfaceY = baseSurfaceY
                         + MaxTerraceRiseDm(plot.Archetype, footprint,
                                           localX, localZ, width, depth) * scale;
            result.Add(new DressingPlacement(kind, world.X, world.Z, surfaceY));
        }

        private static void AddSquare(List<DressingPlacement> result, BuildingPlot plot,
                                      Int3 footprint, DressingKind kind,
                                      int localX, int localZ, int size,
                                      int baseSurfaceY, int scale)
        {
            RectDm world = RotateRect(plot, footprint, localX, localZ, size, size);
            int surfaceY = baseSurfaceY
                         + MaxTerraceRiseDm(plot.Archetype, footprint,
                                           localX, localZ, size, size) * scale;
            result.Add(new DressingPlacement(kind, world.X, world.Z, surfaceY));
        }

        private static int MaxTerraceRiseDm(StructureArchetype archetype, Int3 footprint,
                                            int x, int z, int width, int depth)
        {
            int x1 = x + width - 1;
            int z1 = z + depth - 1;
            int rise = TerraceRiseDm(archetype, footprint, x, z);
            rise = Math.Max(rise, TerraceRiseDm(archetype, footprint, x1, z));
            rise = Math.Max(rise, TerraceRiseDm(archetype, footprint, x, z1));
            rise = Math.Max(rise, TerraceRiseDm(archetype, footprint, x1, z1));
            return rise;
        }

        private static int TerraceRiseDm(StructureArchetype archetype, Int3 footprint,
                                         int x, int z)
        {
            RectDm core = CorePadFor(archetype);
            for (int terrace = 0; terrace <= TerraceCount; terrace++)
            {
                RectDm rect = Expand(core, terrace * TerraceStepDm, footprint);
                if (Contains(rect, x, z)) return terrace * TerraceStepDm;
            }

            return TerraceCount * TerraceStepDm;
        }

        private static bool Contains(RectDm rect, int x, int z) =>
            x >= rect.X && z >= rect.Z
            && x < rect.X + rect.Width
            && z < rect.Z + rect.Depth;

        // Keep the usable core in sync with KentridgePlotSurfaceCatalogue.PadFor. Dressing only
        // needs this small architectural contract; the cut/fill implementation remains in the
        // surface catalogue.
        private static RectDm CorePadFor(StructureArchetype archetype)
        {
            switch (archetype)
            {
                case StructureArchetype.Townhouse: return new RectDm(6, 4, 90, 88);
                case StructureArchetype.WideHouse: return new RectDm(6, 4, 116, 100);
                case StructureArchetype.Shop: return new RectDm(4, 0, 116, 102);
                case StructureArchetype.Inn: return new RectDm(6, 6, 166, 158);
                case StructureArchetype.Warehouse: return new RectDm(7, 10, 182, 174);
                case StructureArchetype.Mansion: return new RectDm(12, 0, 244, 236);
                case StructureArchetype.Church: return new RectDm(12, 8, 140, 148);
                case StructureArchetype.Well: return new RectDm(0, 0, 56, 56);
                default: return new RectDm(4, 4, 96, 96);
            }
        }

        private static RectDm Expand(RectDm source, int amount, Int3 footprint)
        {
            int x0 = math.max(0, source.X - amount);
            int z0 = math.max(0, source.Z - amount);
            int x1 = math.min(footprint.X, source.X + source.Width + amount);
            int z1 = math.min(footprint.Z, source.Z + source.Depth + amount);
            return new RectDm(x0, z0, math.max(1, x1 - x0), math.max(1, z1 - z0));
        }

        private static RectDm RotateRect(BuildingPlot plot, Int3 footprint,
                                         int localX, int localZ, int width, int depth)
        {
            Int2 a = RotatePoint(localX, localZ, footprint, plot.Frontage);
            Int2 b = RotatePoint(localX + width - 1, localZ + depth - 1,
                                 footprint, plot.Frontage);
            int minX = Math.Min(a.X, b.X);
            int minZ = Math.Min(a.Y, b.Y);
            int maxX = Math.Max(a.X, b.X);
            int maxZ = Math.Max(a.Y, b.Y);

            return new RectDm(
                plot.PositionDm.X + minX,
                plot.PositionDm.Y + minZ,
                maxX - minX + 1,
                maxZ - minZ + 1);
        }

        private static Int2 RotatePoint(int x, int z, Int3 footprint,
                                        FrontageDirection frontage)
        {
            int maxX = footprint.X - 1;
            int maxZ = footprint.Z - 1;

            switch (frontage)
            {
                case FrontageDirection.West: return new Int2(maxZ - z, x);
                case FrontageDirection.North: return new Int2(maxX - x, maxZ - z);
                case FrontageDirection.East: return new Int2(z, maxX - x);
                default: return new Int2(x, z);
            }
        }

        private static int PlotSurfaceY(SettlementPlan plan, BuildingPlot plot, uint seed, int scale)
        {
            Int3 footprintDm = SettlementFootprints.For(plan, plot.Archetype);
            int ox = plot.PositionDm.X * scale;
            int oz = plot.PositionDm.Y * scale;
            int width = footprintDm.X * scale;
            int depth = footprintDm.Z * scale;
            int lowest = int.MaxValue;
            int sampleStep = math.max(8, 16 * scale);

            for (int z = 0; z <= depth; z += sampleStep)
            for (int x = 0; x <= width; x += sampleStep)
            {
                int h = TerrainSampler.HeightAt(ox + x, oz + z, seed);
                if (h < lowest) lowest = h;
            }

            return lowest;
        }

        private static int3 Footprint(DressingKind kind, int scale)
        {
            switch (kind)
            {
                case DressingKind.FenceX:
                    return new int3(FenceLengthDm * scale, 18 * scale, FenceDepthDm * scale);
                case DressingKind.FenceZ:
                    return new int3(FenceDepthDm * scale, 18 * scale, FenceLengthDm * scale);
                case DressingKind.GardenBed:
                    return new int3(8 * scale, 8 * scale, 8 * scale);
                case DressingKind.Signpost:
                    return new int3(8 * scale, 32 * scale, 8 * scale);
                case DressingKind.CargoStack:
                    return new int3(14 * scale, 18 * scale, 14 * scale);
                case DressingKind.GraveMarker:
                    return new int3(8 * scale, 18 * scale, 8 * scale);
                case DressingKind.HedgeCluster:
                    return new int3(16 * scale, 18 * scale, 16 * scale);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string KindName(DressingKind kind)
        {
            switch (kind)
            {
                case DressingKind.FenceX: return "fence-x";
                case DressingKind.FenceZ: return "fence-z";
                case DressingKind.GardenBed: return "garden-bed";
                case DressingKind.Signpost: return "signpost";
                case DressingKind.CargoStack: return "cargo-stack";
                case DressingKind.GraveMarker: return "grave-marker";
                case DressingKind.HedgeCluster: return "hedge-cluster";
                default: return "unknown";
            }
        }

        private static int[] FenceXProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            var b = new ProgramBuilder();
            b.Box(0, 0, 0, 4 * s, 16 * s, 4 * s, timber);
            b.Box(14 * s, 0, 0, 4 * s, 16 * s, 4 * s, timber);
            b.Box(28 * s, 0, 0, 4 * s, 16 * s, 4 * s, timber);
            b.Box(0, 5 * s, 1 * s, 32 * s, 2 * s, 2 * s, timber);
            b.Box(0, 11 * s, 1 * s, 32 * s, 2 * s, 2 * s, timber);
            return b.Finish();
        }

        private static int[] FenceZProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            var b = new ProgramBuilder();
            b.Box(0, 0, 0, 4 * s, 16 * s, 4 * s, timber);
            b.Box(0, 0, 14 * s, 4 * s, 16 * s, 4 * s, timber);
            b.Box(0, 0, 28 * s, 4 * s, 16 * s, 4 * s, timber);
            b.Box(1 * s, 5 * s, 0, 2 * s, 2 * s, 32 * s, timber);
            b.Box(1 * s, 11 * s, 0, 2 * s, 2 * s, 32 * s, timber);
            return b.Finish();
        }

        private static int[] GardenProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte green = settings.Materials.Resolve(MaterialRole.Moss);
            var b = new ProgramBuilder();
            b.Box(0, 0, 0, 8 * s, 3 * s, 2 * s, stone);
            b.Box(0, 0, 6 * s, 8 * s, 3 * s, 2 * s, stone);
            b.Box(0, 0, 2 * s, 2 * s, 3 * s, 4 * s, stone);
            b.Box(6 * s, 0, 2 * s, 2 * s, 3 * s, 4 * s, stone);
            b.Box(2 * s, 0, 2 * s, 4 * s, 2 * s, 4 * s, green);
            return b.Finish();
        }

        private static int[] SignpostProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte sign = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Cylinder(4 * s, 0, 4 * s, 3 * s, 3 * s, 1, stone);
            b.Box(3 * s, 2 * s, 3 * s, 3 * s, 25 * s, 3 * s, timber);
            b.Box(0, 18 * s, 2 * s, 8 * s, 7 * s, 2 * s, sign);
            b.Box(2 * s, 18 * s, 0, 2 * s, 7 * s, 8 * s, sign);
            return b.Finish();
        }

        private static int[] CargoProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Box(0, 0, 0, 7 * s, 8 * s, 7 * s, timber);
            b.Box(7 * s, 0, 2 * s, 7 * s, 6 * s, 7 * s, timber);
            b.Box(4 * s, 7 * s, 1 * s, 7 * s, 7 * s, 7 * s, timber);
            b.Cylinder(3 * s, 0, 11 * s, 3 * s, 9 * s, 1, timber);
            b.Cylinder(10 * s, 0, 11 * s, 3 * s, 7 * s, 1, timber);
            b.Box(0, 3 * s, 0, 7 * s, 1 * s, 7 * s, dark);
            return b.Finish();
        }

        private static int[] GraveProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Box(1 * s, 0, 1 * s, 6 * s, 3 * s, 6 * s, stone);
            b.Box(3 * s, 3 * s, 2 * s, 3 * s, 13 * s, 4 * s, stone);
            b.Box(1 * s, 9 * s, 2 * s, 7 * s, 3 * s, 4 * s, stone);
            return b.Finish();
        }

        private static int[] HedgeProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte green = settings.Materials.Resolve(MaterialRole.Moss);
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var b = new ProgramBuilder();
            b.Box(1 * s, 0, 1 * s, 14 * s, 2 * s, 14 * s, stone);
            b.Cylinder(5 * s, 2 * s, 6 * s, 5 * s, 12 * s, 1, green);
            b.Cylinder(11 * s, 2 * s, 6 * s, 5 * s, 13 * s, 1, green);
            b.Cylinder(8 * s, 2 * s, 11 * s, 5 * s, 11 * s, 1, green);
            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, 0, 0, (int)PrimitiveMode.Fill);

            public void Cylinder(int cx, int y, int cz, int radius, int height,
                                 byte axis, byte material) =>
                Op(ShapeOp.EmitCylinder, cx, y, cz, radius, height, axis,
                   material, 0, 0, (int)PrimitiveMode.Fill);

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _code.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(0);
                _code.AddRange(operands);
            }
        }
    }
}
