using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Terrain;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Turns Kentridge's macro height profile into neighbourhood-scale urban shelves.
    ///
    /// The authored rectangle for each district is the flat buildable core. Around that core the
    /// catalogue grows a stepped earth shoulder that descends toward surrounding terrain. The
    /// shoulder material now changes with urban intensity: lower homes retain garden-like ground,
    /// mixed working/inn shelves expose packed earth, and market/upper/civic cores read as paved
    /// hardscape. Crisp retaining walls and stairs are authored separately as Infrastructure.
    /// </summary>
    public static class KentridgeDistrictTerraceCatalogue
    {
        private const int CapThicknessDm = 4;
        private const int BuriedFootingDm = 8;
        private const int ClearAboveDm = 48;
        private const int NaturalSampleStepDm = 64;

        // The core remains the semantic shelf. These 3.6 m shoulders live outside it so building
        // support is unchanged while the silhouette tapers toward adjacent natural terrain.
        private const int ShoulderWidthDm = 36;
        private const int ShoulderLevels = 3;
        private const int ShoulderCapThicknessDm = 2;

        private enum SurfaceCharacter : byte
        {
            Green,
            Mixed,
            Urban,
        }

        private readonly struct TerraceSeed
        {
            public readonly string Id;
            public readonly int XDm;
            public readonly int ZDm;
            public readonly int WidthDm;
            public readonly int DepthDm;
            public readonly int AnchorXDm;
            public readonly int AnchorZDm;
            public readonly SurfaceCharacter Surface;

            public TerraceSeed(string id, int xDm, int zDm, int widthDm, int depthDm,
                               int anchorXDm, int anchorZDm, SurfaceCharacter surface)
            {
                Id = id;
                XDm = xDm;
                ZDm = zDm;
                WidthDm = widthDm;
                DepthDm = depthDm;
                AnchorXDm = anchorXDm;
                AnchorZDm = anchorZDm;
                Surface = surface;
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
            public readonly int ShoulderWidth;

            public TerraceBuild(TerraceSeed seed, int3 position, int3 footprint,
                                int supportHeight, int capThickness, int clearHeight,
                                int shoulderWidth)
            {
                Seed = seed;
                Position = position;
                Footprint = footprint;
                SupportHeight = supportHeight;
                CapThickness = capThickness;
                ClearHeight = clearHeight;
                ShoulderWidth = shoulderWidth;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            TerraceSeed[] seeds =
            {
                new TerraceSeed("lower-residential-main", 620, 900, 800, 190, 1170, 950,
                    SurfaceCharacter.Green),
                new TerraceSeed("lower-residential-east", 1460, 850, 240, 200, 1530, 945,
                    SurfaceCharacter.Green),
                new TerraceSeed("lower-middle", 980, 650, 460, 210, 1222, 760,
                    SurfaceCharacter.Mixed),
                new TerraceSeed("working-yard", 1490, 570, 260, 250, 1530, 700,
                    SurfaceCharacter.Mixed),
                new TerraceSeed("market-main", 680, 440, 620, 260, 1170, 520,
                    SurfaceCharacter.Urban),
                new TerraceSeed("market-rebecca", 1240, 350, 180, 150, 1318, 478,
                    SurfaceCharacter.Urban),
                new TerraceSeed("upper-shoulder", 900, 240, 310, 200, 1118, 340,
                    SurfaceCharacter.Urban),
                new TerraceSeed("civic-summit", 920, 40, 470, 200, 1170, 150,
                    SurfaceCharacter.Urban),
                new TerraceSeed("noble-ridge", 1490, 90, 340, 320, 1530, 250,
                    SurfaceCharacter.Urban),
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
                    // hill first; circulation, infrastructure, parcel grading, and structures refine it.
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
                    MaxPrimitives = 12,
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
            int shoulder = ShoulderWidthDm * scale;

            return new TerraceBuild(
                terrace,
                new int3(
                    (terrace.XDm - ShoulderWidthDm) * scale,
                    originY,
                    (terrace.ZDm - ShoulderWidthDm) * scale),
                new int3(
                    (terrace.WidthDm + ShoulderWidthDm * 2) * scale,
                    totalHeight,
                    (terrace.DepthDm + ShoulderWidthDm * 2) * scale),
                supportHeight,
                capThickness,
                clearHeight,
                shoulder);
        }

        private static void TerrainRange(TerraceSeed terrace, uint seed, int scale,
                                         out int minY, out int maxY)
        {
            minY = int.MaxValue;
            maxY = int.MinValue;

            int minX = terrace.XDm - ShoulderWidthDm;
            int maxX = terrace.XDm + terrace.WidthDm + ShoulderWidthDm;
            int minZ = terrace.ZDm - ShoulderWidthDm;
            int maxZ = terrace.ZDm + terrace.DepthDm + ShoulderWidthDm;

            for (int z = minZ; z <= maxZ; z += NaturalSampleStepDm)
            {
                for (int x = minX; x <= maxX; x += NaturalSampleStepDm)
                    Sample(x, z, seed, scale, ref minY, ref maxY);
                Sample(maxX, z, seed, scale, ref minY, ref maxY);
            }

            for (int x = minX; x <= maxX; x += NaturalSampleStepDm)
                Sample(x, maxZ, seed, scale, ref minY, ref maxY);

            Sample(maxX, maxZ, seed, scale, ref minY, ref maxY);
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
            int s = settings.VoxelsPerDecimetre;
            byte earth = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte moss = settings.Materials.Resolve(MaterialRole.Moss);
            byte paved = settings.Materials.Resolve(MaterialRole.DarkMasonry);

            int coreWidth = build.Seed.WidthDm * s;
            int coreDepth = build.Seed.DepthDm * s;
            int shoulder = build.ShoulderWidth;
            int width = coreWidth + shoulder * 2;
            int depth = coreDepth + shoulder * 2;
            int shoulderCap = Math.Max(1, ShoulderCapThicknessDm * s);

            // High shelves spend most of their artificial rise across the shoulder instead of
            // exposing it as one vertical wall. Low shelves still get at least a modest stepped toe.
            int maximumDrop = Math.Max(0, build.SupportHeight - 1);
            int desiredDrop = Math.Max(12 * s, build.SupportHeight * 2 / 3);
            int shoulderDrop = Math.Min(maximumDrop, desiredDrop);

            int h0 = build.SupportHeight - shoulderDrop;
            int h1 = build.SupportHeight - shoulderDrop * 2 / 3;
            int h2 = build.SupportHeight - shoulderDrop / 3;
            int inset1 = shoulder / ShoulderLevels;
            int inset2 = shoulder * 2 / ShoulderLevels;
            int coreInset = shoulder;

            byte outerCap = moss;
            byte middleCap = build.Seed.Surface == SurfaceCharacter.Green ? moss : earth;
            byte innerCap = build.Seed.Surface == SurfaceCharacter.Green ? moss : earth;
            byte coreCap = build.Seed.Surface switch
            {
                SurfaceCharacter.Green => moss,
                SurfaceCharacter.Mixed => earth,
                _ => paved,
            };

            var b = new ProgramBuilder();

            // Clear from the lowest shoulder level upward, then rebuild the hillside as nested
            // terraces. Only the outer toe stays consistently green. Urban shelves transition
            // through exposed/packed earth into a paved core, eliminating the repeated lawn bands.
            b.Carve(0, h0, 0,
                    width, build.ClearHeight + shoulderDrop + build.CapThickness, depth);

            AddTier(b, 0, 0, width, depth,
                    0, h0, shoulderCap, earth, outerCap);
            AddTier(b, inset1, inset1,
                    width - inset1 * 2, depth - inset1 * 2,
                    h0, h1, shoulderCap, earth, middleCap);
            AddTier(b, inset2, inset2,
                    width - inset2 * 2, depth - inset2 * 2,
                    h1, h2, shoulderCap, earth, innerCap);
            AddTier(b, coreInset, coreInset,
                    coreWidth, coreDepth,
                    h2, build.SupportHeight, build.CapThickness, earth, coreCap);

            return b.Finish();
        }

        private static void AddTier(ProgramBuilder b,
                                    int x, int z, int width, int depth,
                                    int fromY, int toY, int capThickness,
                                    byte earth, byte capMaterial)
        {
            int height = toY - fromY;
            if (height > 0)
                b.Box(x, fromY, z, width, height, depth, earth);

            int cap = Math.Max(1, capThickness);
            b.Box(x, toY, z, width, cap, depth, capMaterial);
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);
            }

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
