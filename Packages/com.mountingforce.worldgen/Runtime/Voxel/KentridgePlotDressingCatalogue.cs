using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Terrain;

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
            SettlementPlan plan = KentridgeDefinition.Build(seed);
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

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
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

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
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

                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                int surfaceY = PlotSurfaceY(plot, seed, scale) + scale;

                switch (plot.District)
                {
                    case DistrictKind.Residential:
                        AddResidential(result, plot, footprint, surfaceY);
                        break;
                    case DistrictKind.Market:
                        AddMarket(result, plot, footprint, surfaceY);
                        break;
                    case DistrictKind.Working:
                        AddWorking(result, plot, footprint, surfaceY);
                        break;
                    case DistrictKind.Noble:
                        AddNoble(result, plot, footprint, surfaceY);
                        break;
                    case DistrictKind.Civic:
                        AddCivic(result, plot, footprint, surfaceY);
                        break;
                }
            }

            if (result.Count < 40)
                throw new InvalidOperationException(
                    "Kentridge plot dressing produced implausibly few placements: " + result.Count);

            return result;
        }

        private static void AddResidential(List<DressingPlacement> result, BuildingPlot plot,
                                           Int3 footprint, int surfaceY)
        {
            // A low rear fence establishes plot ownership without walling off the street frontage.
            AddFence(result, plot, footprint, 8, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, surfaceY);
            AddFence(result, plot, footprint,
                     footprint.X - 8 - FenceLengthDm, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, surfaceY);

            if ((KentridgeRole)plot.RoleId == KentridgeRole.AbandonedHouse)
            {
                // The abandoned lot keeps the same parcel language but reads neglected rather than
                // domestic: goods/debris replace the tended garden.
                AddSquare(result, plot, footprint, DressingKind.CargoStack,
                          8, footprint.Z - 24, 20, surfaceY);
                return;
            }

            int gardenX = (plot.RoleId & 1) == 0 ? 8 : footprint.X - 24;
            AddSquare(result, plot, footprint, DressingKind.GardenBed,
                      gardenX, footprint.Z - 22, 16, surfaceY);
        }

        private static void AddMarket(List<DressingPlacement> result, BuildingPlot plot,
                                      Int3 footprint, int surfaceY)
        {
            // Signs sit beside the public entrance instead of blocking the frontage path itself.
            AddSquare(result, plot, footprint, DressingKind.Signpost,
                      footprint.X - 14, 1, 12, surfaceY);

            // Rear service clutter differentiates the commercial lane from residential gardens.
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      5, footprint.Z - 22, 20, surfaceY);

            // Inns and pubs accumulate more deliveries than the specialist shops.
            if (plot.Archetype == StructureArchetype.Inn)
                AddSquare(result, plot, footprint, DressingKind.CargoStack,
                          footprint.X - 25, footprint.Z - 22, 20, surfaceY);
        }

        private static void AddWorking(List<DressingPlacement> result, BuildingPlot plot,
                                       Int3 footprint, int surfaceY)
        {
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      6, footprint.Z - 22, 20, surfaceY);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      32, footprint.Z - 22, 20, surfaceY);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      footprint.X - 52, footprint.Z - 22, 20, surfaceY);
            AddSquare(result, plot, footprint, DressingKind.CargoStack,
                      footprint.X - 26, footprint.Z - 22, 20, surfaceY);

            AddFence(result, plot, footprint, 5, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, surfaceY);
            AddFence(result, plot, footprint,
                     footprint.X - 5 - FenceLengthDm, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, surfaceY);
        }

        private static void AddNoble(List<DressingPlacement> result, BuildingPlot plot,
                                     Int3 footprint, int surfaceY)
        {
            // A formal rear boundary and clipped hedge rhythm makes Radcliffe's estate read as a
            // different social tier without introducing one-off mansion geometry.
            int[] fenceX = { 24, 82, 140, 198 };
            for (int i = 0; i < fenceX.Length; i++)
                AddFence(result, plot, footprint, fenceX[i], footprint.Z - FenceDepthDm,
                         FenceLengthDm, FenceDepthDm, surfaceY);

            int[] hedgeX = { 30, footprint.X / 2 - 10, footprint.X - 50 };
            for (int i = 0; i < hedgeX.Length; i++)
                AddSquare(result, plot, footprint, DressingKind.HedgeCluster,
                          hedgeX[i], footprint.Z - 28, 20, surfaceY);
        }

        private static void AddCivic(List<DressingPlacement> result, BuildingPlot plot,
                                     Int3 footprint, int surfaceY)
        {
            if (plot.Archetype == StructureArchetype.Church)
            {
                // The church has very little rear setback, so the graveyard occupies the quiet
                // canonical west strip beside the nave and rotates with the church frontage.
                int[] graveZ = { 48, 72, 96, 120 };
                for (int i = 0; i < graveZ.Length; i++)
                    AddSquare(result, plot, footprint, DressingKind.GraveMarker,
                              5, graveZ[i], 10, surfaceY);

                AddFence(result, plot, footprint, 2, 38,
                         FenceDepthDm, FenceLengthDm, surfaceY);
                AddFence(result, plot, footprint, 2, 104,
                         FenceDepthDm, FenceLengthDm, surfaceY);
                return;
            }

            // The mayor's house follows the residential parcel vocabulary but gets two gardens.
            AddFence(result, plot, footprint, 8, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, surfaceY);
            AddFence(result, plot, footprint,
                     footprint.X - 8 - FenceLengthDm, footprint.Z - FenceDepthDm,
                     FenceLengthDm, FenceDepthDm, surfaceY);
            AddSquare(result, plot, footprint, DressingKind.GardenBed,
                      8, footprint.Z - 22, 16, surfaceY);
            AddSquare(result, plot, footprint, DressingKind.GardenBed,
                      footprint.X - 24, footprint.Z - 22, 16, surfaceY);
        }

        private static void AddFence(List<DressingPlacement> result, BuildingPlot plot,
                                     Int3 footprint, int localX, int localZ,
                                     int width, int depth, int surfaceY)
        {
            RectDm world = RotateRect(plot, footprint, localX, localZ, width, depth);
            DressingKind kind = world.Width >= world.Depth
                ? DressingKind.FenceX
                : DressingKind.FenceZ;
            result.Add(new DressingPlacement(kind, world.X, world.Z, surfaceY));
        }

        private static void AddSquare(List<DressingPlacement> result, BuildingPlot plot,
                                      Int3 footprint, DressingKind kind,
                                      int localX, int localZ, int size, int surfaceY)
        {
            RectDm world = RotateRect(plot, footprint, localX, localZ, size, size);
            result.Add(new DressingPlacement(kind, world.X, world.Z, surfaceY));
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

        private static int PlotSurfaceY(BuildingPlot plot, uint seed, int scale)
        {
            Int3 footprintDm = KentridgeDefinition.FootprintDm(plot.Archetype);
            int ox = plot.PositionDm.X * scale;
            int oz = plot.PositionDm.Y * scale;
            int width = footprintDm.X * scale;
            int depth = footprintDm.Z * scale;
            int lowest = int.MaxValue;
            int sampleStep = math.max(8, 16 * scale);

            // Keep this identical to plot grading and building placement. The returned voxel is the
            // green top layer of the prepared core, so callers place props one voxel above it.
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
                    return new int3(16 * scale, 8 * scale, 16 * scale);
                case DressingKind.Signpost:
                    return new int3(12 * scale, 32 * scale, 12 * scale);
                case DressingKind.CargoStack:
                    return new int3(20 * scale, 18 * scale, 20 * scale);
                case DressingKind.GraveMarker:
                    return new int3(10 * scale, 18 * scale, 10 * scale);
                case DressingKind.HedgeCluster:
                    return new int3(20 * scale, 18 * scale, 20 * scale);
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
            b.Box(0, 0, 0, 16 * s, 3 * s, 3 * s, stone);
            b.Box(0, 0, 13 * s, 16 * s, 3 * s, 3 * s, stone);
            b.Box(0, 0, 3 * s, 3 * s, 3 * s, 10 * s, stone);
            b.Box(13 * s, 0, 3 * s, 3 * s, 3 * s, 10 * s, stone);
            b.Box(3 * s, 0, 3 * s, 10 * s, 2 * s, 10 * s, green);
            return b.Finish();
        }

        private static int[] SignpostProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte sign = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Cylinder(6 * s, 0, 6 * s, 3 * s, 3 * s, 1, stone);
            b.Box(5 * s, 2 * s, 5 * s, 3 * s, 25 * s, 3 * s, timber);
            // Crossed boards remain readable no matter which street side the plot occupies.
            b.Box(1 * s, 18 * s, 4 * s, 10 * s, 7 * s, 2 * s, sign);
            b.Box(4 * s, 18 * s, 1 * s, 2 * s, 7 * s, 10 * s, sign);
            return b.Finish();
        }

        private static int[] CargoProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Box(0, 0, 0, 10 * s, 9 * s, 10 * s, timber);
            b.Box(10 * s, 0, 3 * s, 9 * s, 7 * s, 9 * s, timber);
            b.Box(5 * s, 8 * s, 1 * s, 9 * s, 8 * s, 9 * s, timber);
            b.Cylinder(4 * s, 0, 15 * s, 4 * s, 10 * s, 1, timber);
            b.Cylinder(13 * s, 0, 15 * s, 4 * s, 8 * s, 1, timber);
            b.Box(0, 3 * s, 0, 10 * s, 1 * s, 10 * s, dark);
            return b.Finish();
        }

        private static int[] GraveProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();
            b.Box(1 * s, 0, 1 * s, 8 * s, 3 * s, 8 * s, stone);
            b.Box(4 * s, 3 * s, 3 * s, 3 * s, 13 * s, 4 * s, stone);
            b.Box(2 * s, 9 * s, 3 * s, 7 * s, 3 * s, 4 * s, stone);
            return b.Finish();
        }

        private static int[] HedgeProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte green = settings.Materials.Resolve(MaterialRole.Moss);
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var b = new ProgramBuilder();
            b.Box(1 * s, 0, 1 * s, 18 * s, 2 * s, 18 * s, stone);
            b.Cylinder(6 * s, 2 * s, 7 * s, 6 * s, 12 * s, 1, green);
            b.Cylinder(14 * s, 2 * s, 7 * s, 6 * s, 13 * s, 1, green);
            b.Cylinder(10 * s, 2 * s, 14 * s, 6 * s, 11 * s, 1, green);
            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, (int)PrimitiveMode.Fill);

            public void Cylinder(int cx, int y, int cz, int radius, int height,
                                 byte axis, byte material) =>
                Op(ShapeOp.EmitCylinder, cx, y, cz, radius, height, axis,
                   material, (int)PrimitiveMode.Fill);

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
