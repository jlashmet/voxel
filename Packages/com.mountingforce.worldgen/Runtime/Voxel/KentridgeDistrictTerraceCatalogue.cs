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
    /// The authored rectangle for each district is the flat buildable core. Four terrain shoulders
    /// connect that core to the natural hillside. Each shoulder samples its own outer edge altitude
    /// and cuts/fills only between that altitude and the authored core surface, so an elevated
    /// district no longer exposes one shared basement-height wall around its perimeter. Crisp
    /// retaining walls and stairs remain a separate Infrastructure pass.
    /// </summary>
    public static class KentridgeDistrictTerraceCatalogue
    {
        private const int BuriedFootingDm = 8;
        private const int ClearAboveDm = 48;
        private const int NaturalSampleStepDm = 64;
        private const int ShoulderWidthDm = 36;

        private const byte RampAxisX = 0;
        private const byte RampAxisZ = 2;
        private const byte ReverseRampBit = 0x80;

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
            public readonly int CoreSurfaceY;
            public readonly int NorthEdgeY;
            public readonly int SouthEdgeY;
            public readonly int WestEdgeY;
            public readonly int EastEdgeY;

            public TerraceBuild(TerraceSeed seed, int3 position, int3 footprint,
                                int coreSurfaceY, int northEdgeY, int southEdgeY,
                                int westEdgeY, int eastEdgeY)
            {
                Seed = seed;
                Position = position;
                Footprint = footprint;
                CoreSurfaceY = coreSurfaceY;
                NorthEdgeY = northEdgeY;
                SouthEdgeY = southEdgeY;
                WestEdgeY = westEdgeY;
                EastEdgeY = eastEdgeY;
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
                    MaxPrimitives = 18,
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

            int centreXDm = terrace.XDm + terrace.WidthDm / 2;
            int centreZDm = terrace.ZDm + terrace.DepthDm / 2;
            int northEdge = TerrainSampler.HeightAt(
                centreXDm * scale,
                (terrace.ZDm - ShoulderWidthDm) * scale,
                seed);
            int southEdge = TerrainSampler.HeightAt(
                centreXDm * scale,
                (terrace.ZDm + terrace.DepthDm + ShoulderWidthDm) * scale,
                seed);
            int westEdge = TerrainSampler.HeightAt(
                (terrace.XDm - ShoulderWidthDm) * scale,
                centreZDm * scale,
                seed);
            int eastEdge = TerrainSampler.HeightAt(
                (terrace.XDm + terrace.WidthDm + ShoulderWidthDm) * scale,
                centreZDm * scale,
                seed);

            int lowestRelevant = Math.Min(targetSurface,
                Math.Min(Math.Min(northEdge, southEdge), Math.Min(westEdge, eastEdge)));
            int highestRelevant = Math.Max(targetSurface,
                Math.Max(Math.Max(northEdge, southEdge), Math.Max(westEdge, eastEdge)));
            int originY = Math.Min(lowestRelevant, naturalMin) - BuriedFootingDm * scale;
            int topY = Math.Max(highestRelevant, naturalMax) + ClearAboveDm * scale;
            int shoulder = ShoulderWidthDm * scale;

            return new TerraceBuild(
                terrace,
                new int3(
                    (terrace.XDm - ShoulderWidthDm) * scale,
                    originY,
                    (terrace.ZDm - ShoulderWidthDm) * scale),
                new int3(
                    (terrace.WidthDm + ShoulderWidthDm * 2) * scale,
                    Math.Max(1, topY - originY),
                    (terrace.DepthDm + ShoulderWidthDm * 2) * scale),
                targetSurface - originY,
                northEdge - originY,
                southEdge - originY,
                westEdge - originY,
                eastEdge - originY);
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
            int shoulder = ShoulderWidthDm * s;
            int width = coreWidth + shoulder * 2;
            int depth = coreDepth + shoulder * 2;
            int coreInset = shoulder;
            int clearTop = build.Footprint.y;

            byte shoulderSurface = build.Seed.Surface == SurfaceCharacter.Green ? moss : earth;
            byte coreSurface = build.Seed.Surface switch
            {
                SurfaceCharacter.Green => moss,
                SurfaceCharacter.Mixed => earth,
                _ => paved,
            };

            var b = new ProgramBuilder();

            // Only the flat urban core owns a deep support mass. Its vertical faces are hidden by
            // the four shoulder bands rather than reaching the district perimeter.
            b.Carve(coreInset, build.CoreSurfaceY, coreInset,
                    coreWidth, Math.Max(1, clearTop - build.CoreSurfaceY), coreDepth);
            b.Box(coreInset, 0, coreInset,
                  coreWidth, Math.Max(1, build.CoreSurfaceY), coreDepth, earth);

            AddShoulder(b,
                0, 0, width, shoulder,
                build.NorthEdgeY, build.CoreSurfaceY,
                RampAxisZ,
                outerAtNegativeAxis: true,
                clearTop, earth);
            AddShoulder(b,
                0, coreInset + coreDepth, width, shoulder,
                build.SouthEdgeY, build.CoreSurfaceY,
                RampAxisZ,
                outerAtNegativeAxis: false,
                clearTop, earth);
            AddShoulder(b,
                0, coreInset, shoulder, coreDepth,
                build.WestEdgeY, build.CoreSurfaceY,
                RampAxisX,
                outerAtNegativeAxis: true,
                clearTop, earth);
            AddShoulder(b,
                coreInset + coreWidth, coreInset, shoulder, coreDepth,
                build.EastEdgeY, build.CoreSurfaceY,
                RampAxisX,
                outerAtNegativeAxis: false,
                clearTop, earth);

            // Paint only the surfaces this feature owns. Natural terrain outside the four transition
            // bands is untouched, so biome ground cover meets the authored shelf organically.
            b.Box(0, 0, 0, width, clearTop, shoulder,
                  shoulderSurface, PrimitiveMode.PaintSurface);
            b.Box(0, 0, coreInset + coreDepth, width, clearTop, shoulder,
                  shoulderSurface, PrimitiveMode.PaintSurface);
            b.Box(0, 0, coreInset, shoulder, clearTop, coreDepth,
                  shoulderSurface, PrimitiveMode.PaintSurface);
            b.Box(coreInset + coreWidth, 0, coreInset, shoulder, clearTop, coreDepth,
                  shoulderSurface, PrimitiveMode.PaintSurface);
            b.Box(coreInset, 0, coreInset, coreWidth, clearTop, coreDepth,
                  coreSurface, PrimitiveMode.PaintSurface);

            return b.Finish();
        }

        private static void AddShoulder(ProgramBuilder b,
                                        int x, int z, int width, int depth,
                                        int edgeY, int coreY,
                                        byte axis, bool outerAtNegativeAxis,
                                        int clearTop, byte material)
        {
            int lowY = Math.Min(edgeY, coreY);
            int highY = Math.Max(edgeY, coreY);
            int rise = highY - lowY;

            // Preserve everything below the lower of the two endpoints, clear protruding terrain
            // above the desired transition, then refill the exact linear wedge.
            b.Carve(x, lowY, z,
                    width, Math.Max(1, clearTop - lowY), depth);

            if (rise <= 0) return;

            bool risesTowardCore = coreY > edgeY;
            bool coreAtNegativeAxis = !outerAtNegativeAxis;
            bool reverse = risesTowardCore ? coreAtNegativeAxis : outerAtNegativeAxis;
            byte rampAxis = reverse ? (byte)(axis | ReverseRampBit) : axis;

            b.Ramp(x, lowY, z,
                   width, rise, depth,
                   rampAxis, material);
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);
            }

            public void Ramp(int x, int y, int z, int sx, int sy, int sz,
                             byte axis, byte material,
                             PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitRamp, x, y, z, sx, sy, sz,
                   axis, material, 0, 0, (int)mode);
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
