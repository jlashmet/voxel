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
    /// Adds small-scale authored life to Kentridge's public spaces without turning the semantic
    /// town plan into a list of renderer prefabs. Dressing is derived from the market-square
    /// relationship already owned by <see cref="KentridgeTownPlanner"/> and compiles to the same
    /// feature bytecode as roads and buildings.
    ///
    /// The first pass deliberately concentrates on the square: four covered stalls, benches,
    /// lantern posts, and goods crates occupy the quiet quadrants around the crossing while keeping
    /// both through-roads and the central well clear. Later passes can extend the same pattern to
    /// shop signs, warehouse yards, gardens, fences, and district-specific clutter.
    /// </summary>
    public static class KentridgeTownDressingCatalogue
    {
        private enum DressingKind : byte
        {
            MarketStall = 0,
            Bench = 1,
            LanternPost = 2,
            MarketCrates = 3,
        }

        private const int DefinitionCount = 4;
        private const int MarketStallCount = 4;
        private const int BenchCount = 4;
        private const int LanternCount = 8;
        private const int CrateCount = 4;
        private const int PlacementCount = MarketStallCount + BenchCount + LanternCount + CrateCount;

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

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            int scale = settings.VoxelsPerDecimetre;
            List<DressingPlacement> placements = BuildPlacements(plan, seed, scale);

            if (placements.Count != PlacementCount)
                throw new InvalidOperationException(
                    "Kentridge market dressing produced an unexpected placement count: "
                    + placements.Count);

            int[][] programs =
            {
                MarketStallProgram(settings),
                BenchProgram(settings),
                LanternProgram(settings),
                CrateProgram(settings),
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
                    Name = new FixedString64Bytes("kentridge-dressing-" + KindName(kind)),
                    // The engine has no decoration feature kind yet. Landform keeps these instances
                    // out of the semantic building count while still using the ordinary feature VM.
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = Footprint(kind, scale),
                    MaxSlope = 32,
                    // Paths are precedence 60 and structures start at 100. Dressing intentionally
                    // lives between them so a building wins any accidental edge overlap.
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
                    "Kentridge town dressing catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static List<DressingPlacement> BuildPlacements(
            SettlementPlan plan, uint seed, int scale)
        {
            PlannedPlaza plaza = plan.Plaza;
            int cx = plaza.CentreDm.X;
            int cz = plaza.CentreDm.Y;
            int minX = cx - plaza.SizeDm.X / 2;
            int maxX = cx + plaza.SizeDm.X / 2;
            int minZ = cz - plaza.SizeDm.Y / 2;
            int maxZ = cz + plaza.SizeDm.Y / 2;

            // The public-space pass flattens the market square to the centre sample. Reusing that
            // semantic target means every prop sits exactly on the generated plaza instead of
            // consulting the pre-grade terrain beneath its individual column.
            int plazaY = TerrainQuery.HeightAt(cx * scale, cz * scale, seed);

            var result = new List<DressingPlacement>(PlacementCount);

            // Four market stalls occupy the square's corner quadrants. The main N/S spine and
            // market street remain unobstructed through the middle, as does the central well.
            Add(result, DressingKind.MarketStall, minX + 12, minZ + 8, plazaY);
            Add(result, DressingKind.MarketStall, maxX - 66, minZ + 8, plazaY);
            Add(result, DressingKind.MarketStall, minX + 12, maxZ - 34, plazaY);
            Add(result, DressingKind.MarketStall, maxX - 66, maxZ - 34, plazaY);

            // Benches face the activity from the road-side edge of each market quadrant.
            Add(result, DressingKind.Bench, minX + 14, minZ + 36, plazaY);
            Add(result, DressingKind.Bench, maxX - 60, minZ + 36, plazaY);
            Add(result, DressingKind.Bench, minX + 14, maxZ - 44, plazaY);
            Add(result, DressingKind.Bench, maxX - 60, maxZ - 44, plazaY);

            // Four lamps frame the actual road crossing and four mark the outer square corners.
            Add(result, DressingKind.LanternPost, cx - 42, cz - 38, plazaY);
            Add(result, DressingKind.LanternPost, cx + 33, cz - 38, plazaY);
            Add(result, DressingKind.LanternPost, cx - 42, cz + 29, plazaY);
            Add(result, DressingKind.LanternPost, cx + 33, cz + 29, plazaY);
            Add(result, DressingKind.LanternPost, minX + 4, minZ + 2, plazaY);
            Add(result, DressingKind.LanternPost, maxX - 13, minZ + 2, plazaY);
            Add(result, DressingKind.LanternPost, minX + 4, maxZ - 11, plazaY);
            Add(result, DressingKind.LanternPost, maxX - 13, maxZ - 11, plazaY);

            // A small stack of goods beside each stall keeps the silhouettes from looking like
            // four cloned kiosks while remaining deterministic and cheap to rasterise.
            Add(result, DressingKind.MarketCrates, minX + 48, minZ + 12, plazaY);
            Add(result, DressingKind.MarketCrates, maxX - 30, minZ + 12, plazaY);
            Add(result, DressingKind.MarketCrates, minX + 48, maxZ - 30, plazaY);
            Add(result, DressingKind.MarketCrates, maxX - 30, maxZ - 30, plazaY);

            return result;
        }

        private static void Add(List<DressingPlacement> list, DressingKind kind,
                                int xDm, int zDm, int surfaceY) =>
            list.Add(new DressingPlacement(kind, xDm, zDm, surfaceY));

        private static int3 Footprint(DressingKind kind, int scale)
        {
            switch (kind)
            {
                case DressingKind.MarketStall: return new int3(34 * scale, 36 * scale, 26 * scale);
                case DressingKind.Bench: return new int3(28 * scale, 18 * scale, 8 * scale);
                case DressingKind.LanternPost: return new int3(9 * scale, 40 * scale, 9 * scale);
                case DressingKind.MarketCrates: return new int3(18 * scale, 18 * scale, 14 * scale);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string KindName(DressingKind kind)
        {
            switch (kind)
            {
                case DressingKind.MarketStall: return "market-stall";
                case DressingKind.Bench: return "bench";
                case DressingKind.LanternPost: return "lantern-post";
                case DressingKind.MarketCrates: return "market-crates";
                default: return "unknown";
            }
        }

        private static int[] MarketStallProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte cloth = settings.Materials.Resolve(MaterialRole.Cloth);
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var b = new ProgramBuilder();

            // Slight stone shoes keep posts visually planted against the flat plaza surface.
            b.Box(1 * s, 0, 1 * s, 5 * s, 3 * s, 5 * s, stone);
            b.Box(28 * s, 0, 1 * s, 5 * s, 3 * s, 5 * s, stone);
            b.Box(1 * s, 0, 20 * s, 5 * s, 3 * s, 5 * s, stone);
            b.Box(28 * s, 0, 20 * s, 5 * s, 3 * s, 5 * s, stone);

            b.Box(2 * s, 2 * s, 2 * s, 3 * s, 23 * s, 3 * s, timber);
            b.Box(29 * s, 2 * s, 2 * s, 3 * s, 23 * s, 3 * s, timber);
            b.Box(2 * s, 2 * s, 21 * s, 3 * s, 23 * s, 3 * s, timber);
            b.Box(29 * s, 2 * s, 21 * s, 3 * s, 23 * s, 3 * s, timber);
            b.Box(3 * s, 8 * s, 3 * s, 28 * s, 5 * s, 10 * s, timber);

            b.Prism(0, 24 * s, 0, 34 * s, 10 * s, 26 * s,
                    PrismProfile.Gable, cloth);
            return b.Finish();
        }

        private static int[] BenchProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var b = new ProgramBuilder();

            b.Box(3 * s, 0, 2 * s, 4 * s, 6 * s, 4 * s, stone);
            b.Box(21 * s, 0, 2 * s, 4 * s, 6 * s, 4 * s, stone);
            b.Box(2 * s, 6 * s, 1 * s, 24 * s, 3 * s, 6 * s, timber);
            b.Box(3 * s, 8 * s, 6 * s, 3 * s, 8 * s, 2 * s, timber);
            b.Box(22 * s, 8 * s, 6 * s, 3 * s, 8 * s, 2 * s, timber);
            b.Box(3 * s, 12 * s, 6 * s, 22 * s, 4 * s, 2 * s, timber);
            return b.Finish();
        }

        private static int[] LanternProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte warm = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte slate = settings.Materials.Resolve(MaterialRole.Slate);
            var b = new ProgramBuilder();

            b.Cylinder(4 * s, 0, 4 * s, 3 * s, 4 * s, 1, stone);
            b.Box(3 * s, 3 * s, 3 * s, 3 * s, 25 * s, 3 * s, timber);
            b.Box(1 * s, 27 * s, 1 * s, 7 * s, 7 * s, 7 * s, warm);
            b.Prism(0, 34 * s, 0, 9 * s, 4 * s, 9 * s,
                    PrismProfile.Gable, slate);
            return b.Finish();
        }

        private static int[] CrateProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            b.Box(0, 0, 0, 10 * s, 9 * s, 10 * s, timber);
            b.Box(9 * s, 0, 3 * s, 9 * s, 7 * s, 9 * s, timber);
            b.Box(4 * s, 8 * s, 2 * s, 9 * s, 8 * s, 9 * s, timber);
            // Thin dark bands make the three-box pile read at street-camera distance.
            b.Box(0, 3 * s, 0, 10 * s, 1 * s, 10 * s, dark);
            b.Box(4 * s, 11 * s, 2 * s, 9 * s, 1 * s, 9 * s, dark);
            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, 0, 0, (int)PrimitiveMode.Fill);

            public void Prism(int x, int y, int z, int sx, int sy, int sz,
                              PrismProfile profile, byte material) =>
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                   (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);

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