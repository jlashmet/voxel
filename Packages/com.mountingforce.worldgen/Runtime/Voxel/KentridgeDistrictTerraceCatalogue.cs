using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Terrain;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Turns Kentridge's macro height profile into broad urban shelves rather than sixteen isolated
    /// raised parcels. Each shelf owns a whole neighbourhood-sized piece of hillside: it cuts away
    /// terrain above an authored elevation, fills missing mass with masonry, and caps the result with
    /// the same green ground vocabulary used around the town.
    ///
    /// Gaps between shelves are intentional. The north/south road and stair passes occupy those
    /// ascent zones, so the player reads a sequence of district terrace -> climb -> district terrace
    /// instead of one enormous rectangular mesa.
    /// </summary>
    public static class KentridgeDistrictTerraceCatalogue
    {
        private const int CapThicknessDm = 4;
        private const int BuriedFootingDm = 8;
        private const int ClearAboveDm = 48;
        private const int NaturalSampleStepDm = 64;

        private readonly struct TerraceSeed
        {
            public readonly string Id;
            public readonly int XDm;
            public readonly int ZDm;
            public readonly int WidthDm;
            public readonly int DepthDm;
            public readonly int AnchorXDm;
            public readonly int AnchorZDm;

            public TerraceSeed(string id, int xDm, int zDm, int widthDm, int depthDm,
                               int anchorXDm, int anchorZDm)
            {
                Id = id;
                XDm = xDm;
                ZDm = zDm;
                WidthDm = widthDm;
                DepthDm = depthDm;
                AnchorXDm = anchorXDm;
                AnchorZDm = anchorZDm;
            }
        }

        private readonly struct TerraceBuild
        {
            public readonly TerraceSeed Seed;
            public readonly int3 Position;
            public readonly int3 Footprint;
            public readonly int SupportHeight;
            public readonly int CapThickness;
            public readonly int ClearHeight;

            public TerraceBuild(TerraceSeed seed, int3 position, int3 footprint,
                                int supportHeight, int capThickness, int clearHeight)
            {
                Seed = seed;
                Position = position;
                Footprint = footprint;
                SupportHeight = supportHeight;
                CapThickness = capThickness;
                ClearHeight = clearHeight;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            TerraceSeed[] seeds =
            {
                // Broad lower shelf carrying the residential street and its four southern homes.
                new TerraceSeed("lower-residential", 650, 850, 980, 230, 1170, 950),

                // A smaller landing around Logan/Pub before the next climb into the market.
                new TerraceSeed("lower-middle", 850, 690, 700, 150, 1170, 760),

                // The market square and shop row should read as one continuous urban platform.
                new TerraceSeed("market", 690, 440, 970, 180, 1170, 520),

                // Inn/upper-town shoulder: deliberately narrower so stone faces remain visible.
                new TerraceSeed("upper-shoulder", 840, 285, 730, 140, 1170, 340),

                // Church and mayor share the highest civic shelf.
                new TerraceSeed("civic-summit", 800, 70, 610, 170, 1170, 150),

                // Radcliffe's estate occupies a separate east-side ridge rather than the civic slab.
                new TerraceSeed("noble-ridge", 1410, 160, 360, 180, 1490, 250),
            };

            var builds = new TerraceBuild[seeds.Length];
            var programs = new int[seeds.Length][];
            int programLength = 0;

            for (int i = 0; i < seeds.Length; i++)
            {
                builds[i] = Resolve(seeds[i], seed, scale);
                programs[i] = TerraceProgram(builds[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
                definitions: seeds.Length,
                rules: seeds.Length,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: seeds.Length,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < builds.Length; i++)
            {
                TerraceBuild build = builds[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-district-terrace-" + build.Seed.Id),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = build.Footprint,
                    MaxSlope = 32,
                    // Ground cover is 5 and authored roads start at 20. District landform owns the
                    // hill first; circulation, parcel grading, and structures refine it afterward.
                    Precedence = 15,
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
                    MaxPrimitives = 3,
                };

                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = build.Position,
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };

                catalogue.Rules[i] = new PlacementRule
                {
                    DefinitionId = i,
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
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };

                programOffset += program.Length;
            }

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge district terrace catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static TerraceBuild Resolve(TerraceSeed terrace, uint seed, int scale)
        {
            int targetSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                terrace.AnchorXDm, terrace.AnchorZDm, seed, scale);
            TerrainRange(terrace, seed, scale, out int naturalMin, out int naturalMax);

            int capThickness = CapThicknessDm * scale;
            int buriedFooting = BuriedFootingDm * scale;
            int capBase = targetSurface - capThickness;
            int originY = Math.Min(capBase - buriedFooting, naturalMin - buriedFooting);
            int supportHeight = Math.Max(1, capBase - originY);
            int clearHeight = Math.Max(
                ClearAboveDm * scale,
                naturalMax - targetSurface + ClearAboveDm * scale);
            int totalHeight = targetSurface - originY + clearHeight;

            return new TerraceBuild(
                terrace,
                new int3(terrace.XDm * scale, originY, terrace.ZDm * scale),
                new int3(terrace.WidthDm * scale, totalHeight, terrace.DepthDm * scale),
                supportHeight,
                capThickness,
                clearHeight);
        }

        private static void TerrainRange(TerraceSeed terrace, uint seed, int scale,
                                         out int minY, out int maxY)
        {
            minY = int.MaxValue;
            maxY = int.MinValue;

            for (int z = terrace.ZDm; z <= terrace.ZDm + terrace.DepthDm;
                 z += NaturalSampleStepDm)
            {
                for (int x = terrace.XDm; x <= terrace.XDm + terrace.WidthDm;
                     x += NaturalSampleStepDm)
                {
                    Sample(x, z, seed, scale, ref minY, ref maxY);
                }
                Sample(terrace.XDm + terrace.WidthDm, z, seed, scale, ref minY, ref maxY);
            }

            for (int x = terrace.XDm; x <= terrace.XDm + terrace.WidthDm;
                 x += NaturalSampleStepDm)
                Sample(x, terrace.ZDm + terrace.DepthDm, seed, scale, ref minY, ref maxY);

            Sample(terrace.XDm + terrace.WidthDm,
                   terrace.ZDm + terrace.DepthDm,
                   seed, scale, ref minY, ref maxY);
            Sample(terrace.AnchorXDm, terrace.AnchorZDm,
                   seed, scale, ref minY, ref maxY);
        }

        private static void Sample(int xDm, int zDm, uint seed, int scale,
                                   ref int minY, ref int maxY)
        {
            int y = TerrainSampler.HeightAt(xDm * scale, zDm * scale, seed);
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        private static int[] TerraceProgram(TerraceBuild build,
                                            VoxelWorldGenSettings settings)
        {
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte ground = settings.Materials.Resolve(MaterialRole.Moss);
            int width = build.Seed.WidthDm * settings.VoxelsPerDecimetre;
            int depth = build.Seed.DepthDm * settings.VoxelsPerDecimetre;
            var b = new ProgramBuilder();

            // Excavate first so the rebuilt shelf remains authoritative within this instance.
            b.Carve(0, build.SupportHeight + build.CapThickness, 0,
                    width, build.ClearHeight, depth);
            b.Box(0, 0, 0,
                  width, build.SupportHeight, depth, stone);
            b.Box(0, build.SupportHeight, 0,
                  width, build.CapThickness, depth, ground);
            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

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
