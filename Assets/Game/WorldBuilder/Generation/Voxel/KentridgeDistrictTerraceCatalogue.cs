using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Turns Kentridge's macro height profile into neighbourhood-scale urban shelves.
    /// District character controls transition depth: green edges stay compact while dense urban
    /// shelves receive broad sloped contour transitions that meet the authored core continuously.
    /// Urban shoulders also compile a sparse crisp masonry retaining skin on their strongest downhill
    /// edges so the vertical city reads as built hillside infrastructure rather than brown plinths.
    /// </summary>
    public static class KentridgeDistrictTerraceCatalogue
    {
        private const int BuriedFootingDm = 8;
        private const int ClearAboveDm = 48;
        private const int NaturalSampleStepDm = 64;
        private const int GreenShoulderWidthDm = 36;
        private const int MixedShoulderWidthDm = 54;
        private const int UrbanShoulderWidthDm = 72;
        private const int ShoulderStepCount = 6;
        private const int UpperWestProfileStepDm = 25;
        private const int RetainingTierStride = 3;
        private const int RetainingFaceThicknessDm = 3;
        private const int RetainingEndInsetDm = 12;
        private const int MinRetainingRiseDm = 10;
        private const int MaxRetainingEdges = 2;

        private const byte AxisX = 0;
        private const byte AxisZ = 2;

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

            public int ShoulderWidthDm => Surface switch
            {
                SurfaceCharacter.Green => GreenShoulderWidthDm,
                SurfaceCharacter.Mixed => MixedShoulderWidthDm,
                _ => UrbanShoulderWidthDm,
            };
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
            public readonly int[] WestEdgeProfileY;

            public TerraceBuild(TerraceSeed seed, int3 position, int3 footprint,
                                int coreSurfaceY, int northEdgeY, int southEdgeY,
                                int westEdgeY, int eastEdgeY, int[] westEdgeProfileY)
            {
                Seed = seed;
                Position = position;
                Footprint = footprint;
                CoreSurfaceY = coreSurfaceY;
                NorthEdgeY = northEdgeY;
                SouthEdgeY = southEdgeY;
                WestEdgeY = westEdgeY;
                EastEdgeY = eastEdgeY;
                WestEdgeProfileY = westEdgeProfileY;
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
                new TerraceSeed("noble-ridge", 1490, 90, 340, 320, 72, 1530,
                    SurfaceCharacter.Urban),
            };

            var builds = new TerraceBuild[seeds.Length];
            var terrainPrograms = new int[seeds.Length][];
            var retainingPrograms = new int[seeds.Length][];
            int programLength = 0;
            int retainingCount = 0;

            for (int i = 0; i < seeds.Length; i++)
            {
                builds[i] = Resolve(seeds[i], seed, scale);
                terrainPrograms[i] = TerraceProgram(builds[i], settings);
                programLength += terrainPrograms[i].Length;

                if (seeds[i].Surface == SurfaceCharacter.Urban)
                {
                    retainingPrograms[i] = RetainingFacingProgram(builds[i], settings);
                    programLength += retainingPrograms[i].Length;
                    retainingCount++;
                }
            }

            int definitionCount = seeds.Length + retainingCount;
            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: definitionCount,
                rules: definitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: definitionCount,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < builds.Length; i++)
            {
                TerraceBuild build = builds[i];
                int[] program = terrainPrograms[i];
                CopyProgram(ref catalogue, programOffset, program);

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
                    MaxPrimitives = 40,
                };

                catalogue.ExplicitPlacements[i] = Placement(build.Position);
                catalogue.Rules[i] = ExplicitRule(i, i);
                programOffset += program.Length;
            }

            int retainingIndex = 0;
            for (int i = 0; i < builds.Length; i++)
            {
                TerraceBuild build = builds[i];
                if (build.Seed.Surface != SurfaceCharacter.Urban) continue;

                int definitionId = seeds.Length + retainingIndex;
                int[] program = retainingPrograms[i];
                CopyProgram(ref catalogue, programOffset, program);

                catalogue.Definitions[definitionId] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-district-retaining-" + build.Seed.Id),
                    Kind = FeatureKind.Infrastructure,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = build.Footprint,
                    MaxSlope = 32,
                    // Above the smooth district mass, below roads. A road penetrating a terrace
                    // therefore cuts through the retaining skin rather than receiving a wall across it.
                    Precedence = 18,
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
                    MaxPrimitives = 16,
                };

                catalogue.ExplicitPlacements[definitionId] = Placement(build.Position);
                catalogue.Rules[definitionId] = ExplicitRule(definitionId, definitionId);
                programOffset += program.Length;
                retainingIndex++;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge district terrace catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static ExplicitPlacement Placement(int3 position)
        {
            return new ExplicitPlacement
            {
                Position = position,
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
        }

        private static PlacementRule ExplicitRule(int definitionId, int placementOffset)
        {
            return new PlacementRule
            {
                DefinitionId = definitionId,
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
                ExplicitCount = 1,
            };
        }

        private static TerraceBuild Resolve(TerraceSeed terrace, uint seed, int scale)
        {
            int targetSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                terrace.AnchorXDm, terrace.AnchorZDm, seed, scale);
            TerrainRange(terrace, seed, scale, out int naturalMin, out int naturalMax);

            int shoulderDm = terrace.ShoulderWidthDm;
            int centreXDm = terrace.XDm + terrace.WidthDm / 2;
            int centreZDm = terrace.ZDm + terrace.DepthDm / 2;
            int northEdge = TerrainQuery.HeightAt(
                centreXDm * scale,
                (terrace.ZDm - shoulderDm) * scale,
                seed);
            int southEdge = TerrainQuery.HeightAt(
                centreXDm * scale,
                (terrace.ZDm + terrace.DepthDm + shoulderDm) * scale,
                seed);
            int westEdge = TerrainQuery.HeightAt(
                (terrace.XDm - shoulderDm) * scale,
                centreZDm * scale,
                seed);
            int eastEdge = TerrainQuery.HeightAt(
                (terrace.XDm + terrace.WidthDm + shoulderDm) * scale,
                centreZDm * scale,
                seed);

            int[] westProfile = null;
            if (terrace.Id == "upper-shoulder")
            {
                int count = (terrace.DepthDm + UpperWestProfileStepDm - 1)
                          / UpperWestProfileStepDm;
                westProfile = new int[count];
                int westXDm = terrace.XDm - shoulderDm;
                for (int i = 0; i < count; i++)
                {
                    int startDm = i * UpperWestProfileStepDm;
                    int depthDm = Math.Min(UpperWestProfileStepDm,
                                           terrace.DepthDm - startDm);
                    int sampleZDm = terrace.ZDm + startDm + depthDm / 2;
                    westProfile[i] = TerrainQuery.HeightAt(
                        westXDm * scale, sampleZDm * scale, seed);
                }
            }

            int lowestRelevant = Math.Min(targetSurface,
                Math.Min(Math.Min(northEdge, southEdge), Math.Min(westEdge, eastEdge)));
            int highestRelevant = Math.Max(targetSurface,
                Math.Max(Math.Max(northEdge, southEdge), Math.Max(westEdge, eastEdge)));
            if (westProfile != null)
            {
                for (int i = 0; i < westProfile.Length; i++)
                {
                    lowestRelevant = Math.Min(lowestRelevant, westProfile[i]);
                    highestRelevant = Math.Max(highestRelevant, westProfile[i]);
                }
            }

            int originY = Math.Min(lowestRelevant, naturalMin) - BuriedFootingDm * scale;
            int topY = Math.Max(highestRelevant, naturalMax) + ClearAboveDm * scale;
            if (westProfile != null)
            {
                for (int i = 0; i < westProfile.Length; i++)
                    westProfile[i] -= originY;
            }

            return new TerraceBuild(
                terrace,
                new int3(
                    (terrace.XDm - shoulderDm) * scale,
                    originY,
                    (terrace.ZDm - shoulderDm) * scale),
                new int3(
                    (terrace.WidthDm + shoulderDm * 2) * scale,
                    Math.Max(1, topY - originY),
                    (terrace.DepthDm + shoulderDm * 2) * scale),
                targetSurface - originY,
                northEdge - originY,
                southEdge - originY,
                westEdge - originY,
                eastEdge - originY,
                westProfile);
        }

        private static void TerrainRange(TerraceSeed terrace, uint seed, int scale,
                                         out int minY, out int maxY)
        {
            minY = int.MaxValue;
            maxY = int.MinValue;

            int shoulderDm = terrace.ShoulderWidthDm;
            int minX = terrace.XDm - shoulderDm;
            int maxX = terrace.XDm + terrace.WidthDm + shoulderDm;
            int minZ = terrace.ZDm - shoulderDm;
            int maxZ = terrace.ZDm + terrace.DepthDm + shoulderDm;

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
            int y = TerrainQuery.HeightAt(xDm * scale, zDm * scale, seed);
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
            int shoulder = build.Seed.ShoulderWidthDm * s;
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

            b.Carve(coreInset, build.CoreSurfaceY, coreInset,
                    coreWidth, Math.Max(1, clearTop - build.CoreSurfaceY), coreDepth);
            b.Box(coreInset, 0, coreInset,
                  coreWidth, Math.Max(1, build.CoreSurfaceY), coreDepth, earth);

            AddShoulder(b,
                0, 0, width, shoulder,
                build.NorthEdgeY, build.CoreSurfaceY,
                AxisZ,
                outerAtNegativeAxis: true,
                clearTop, earth);
            AddShoulder(b,
                0, coreInset + coreDepth, width, shoulder,
                build.SouthEdgeY, build.CoreSurfaceY,
                AxisZ,
                outerAtNegativeAxis: false,
                clearTop, earth);

            if (build.WestEdgeProfileY != null && build.WestEdgeProfileY.Length > 0)
            {
                int stripDepth = UpperWestProfileStepDm * s;
                for (int i = 0; i < build.WestEdgeProfileY.Length; i++)
                {
                    int z = coreInset + i * stripDepth;
                    int depthForStrip = Math.Min(stripDepth,
                        coreInset + coreDepth - z);
                    AddShoulder(b,
                        0, z, shoulder, depthForStrip,
                        build.WestEdgeProfileY[i], build.CoreSurfaceY,
                        AxisX,
                        outerAtNegativeAxis: true,
                        clearTop, earth);
                }
            }
            else
            {
                AddShoulder(b,
                    0, coreInset, shoulder, coreDepth,
                    build.WestEdgeY, build.CoreSurfaceY,
                    AxisX,
                    outerAtNegativeAxis: true,
                    clearTop, earth);
            }

            AddShoulder(b,
                coreInset + coreWidth, coreInset, shoulder, coreDepth,
                build.EastEdgeY, build.CoreSurfaceY,
                AxisX,
                outerAtNegativeAxis: false,
                clearTop, earth);

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

        private static int[] RetainingFacingProgram(
            TerraceBuild build,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            int coreWidth = build.Seed.WidthDm * s;
            int coreDepth = build.Seed.DepthDm * s;
            int shoulder = build.Seed.ShoulderWidthDm * s;
            int width = coreWidth + shoulder * 2;
            int coreInset = shoulder;
            var b = new ProgramBuilder();

            // Only edges where the authored core stands materially above natural terrain need a
            // retaining face. Pick the two strongest rises so a terrace never becomes a boxed fort.
            int[] rises =
            {
                build.CoreSurfaceY - build.NorthEdgeY,
                build.CoreSurfaceY - build.SouthEdgeY,
                build.CoreSurfaceY - build.WestEdgeY,
                build.CoreSurfaceY - build.EastEdgeY,
            };

            int minimumRise = MinRetainingRiseDm * s;
            for (int selected = 0; selected < MaxRetainingEdges; selected++)
            {
                int bestEdge = -1;
                int bestRise = minimumRise - 1;
                for (int edge = 0; edge < rises.Length; edge++)
                {
                    if (rises[edge] > bestRise)
                    {
                        bestRise = rises[edge];
                        bestEdge = edge;
                    }
                }

                if (bestEdge < 0) break;
                rises[bestEdge] = int.MinValue;

                switch (bestEdge)
                {
                    case 0:
                        AddRetainingTiers(
                            b, 0, 0, width, shoulder,
                            build.NorthEdgeY, build.CoreSurfaceY,
                            AxisZ, true, s, stone);
                        break;
                    case 1:
                        AddRetainingTiers(
                            b, 0, coreInset + coreDepth, width, shoulder,
                            build.SouthEdgeY, build.CoreSurfaceY,
                            AxisZ, false, s, stone);
                        break;
                    case 2:
                        AddRetainingTiers(
                            b, 0, coreInset, shoulder, coreDepth,
                            build.WestEdgeY, build.CoreSurfaceY,
                            AxisX, true, s, stone);
                        break;
                    case 3:
                        AddRetainingTiers(
                            b, coreInset + coreWidth, coreInset, shoulder, coreDepth,
                            build.EastEdgeY, build.CoreSurfaceY,
                            AxisX, false, s, stone);
                        break;
                }
            }
            return b.Finish();
        }

        private static void AddShoulder(ProgramBuilder b,
                                        int x, int z, int width, int depth,
                                        int edgeY, int coreY,
                                        byte axis, bool outerAtNegativeAxis,
                                        int clearTop, byte material)
        {
            int lowY = Math.Min(edgeY, coreY);
            int rise = Math.Abs(coreY - edgeY);

            b.Carve(x, lowY, z,
                    width, Math.Max(1, clearTop - lowY), depth);

            if (rise <= 0) return;

            // A single authoritative wedge preserves the exact authored edge/core elevations while
            // removing the six metre-scale plateaus that were visible at Dirt/grass boundaries.
            // ReverseRampBit means the high end lies at the negative side of the selected axis.
            bool highAtNegativeAxis = (coreY > edgeY) != outerAtNegativeAxis;
            byte rampAxis = (byte)(axis
                | (highAtNegativeAxis ? ShapeOps.ReverseRampBit : 0));
            b.Ramp(x, lowY, z, width, rise, depth, rampAxis, material);
        }

        private static void AddRetainingTiers(
            ProgramBuilder b,
            int x, int z, int width, int depth,
            int edgeY, int coreY,
            byte axis, bool outerAtNegativeAxis,
            int scale,
            byte stone)
        {
            if (coreY - edgeY < MinRetainingRiseDm * scale) return;

            int axisLength = axis == AxisX ? width : depth;
            int wallThickness = RetainingFaceThicknessDm * scale;
            int endInset = RetainingEndInsetDm * scale;

            for (int endStep = RetainingTierStride;
                 endStep <= ShoulderStepCount;
                 endStep += RetainingTierStride)
            {
                int startStep = endStep - RetainingTierStride;
                int previousY = edgeY
                    + (coreY - edgeY) * startStep / ShoulderStepCount;
                int targetY = edgeY
                    + (coreY - edgeY) * endStep / ShoulderStepCount;
                int faceHeight = targetY - previousY;
                if (faceHeight <= 0) continue;

                int end = axisLength * endStep / ShoulderStepCount;
                int boundary = outerAtNegativeAxis
                    ? end - wallThickness
                    : axisLength - end;
                boundary = Math.Max(0, Math.Min(axisLength - wallThickness, boundary));

                if (axis == AxisX)
                {
                    int spanZ = Math.Max(1, depth - endInset * 2);
                    b.Box(x + boundary, previousY, z + endInset,
                        wallThickness, faceHeight, spanZ, stone);
                }
                else
                {
                    int spanX = Math.Max(1, width - endInset * 2);
                    b.Box(x + endInset, previousY, z + boundary,
                        spanX, faceHeight, wallThickness, stone);
                }
            }
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++)
                catalogue.Program[offset + i] = program[i];
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

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public void Ramp(int x, int y, int z, int sx, int sy, int sz,
                             byte axis, byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitRamp, x, y, z, sx, sy, sz,
                   axis, material, 0, 0, (int)PrimitiveMode.Fill);
            }

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
