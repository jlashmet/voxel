using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Sparse street-visible furnishing for Kentridge.
    ///
    /// Unlike the market-square dressing, these props are aligned to the semantic street network
    /// and sit at the authored public-space surface at their own column. The placement rhythm is
    /// deliberate: lamps mark the climb and major side streets, benches create civic/residential
    /// pauses, and planters strengthen the commercial frontage without blocking doors or roads.
    /// </summary>
    public static class KentridgeStreetDressingCatalogue
    {
        private enum StreetPropKind : byte
        {
            Lamp = 0,
            Bench = 1,
            Planter = 2,
        }

        private const int DefinitionCount = 3;
        private const int ExpectedPlacementCount = 30;

        // The captured east-market lamp is outside the carriageway but inside the north shoulder of
        // the working-yard district terrace. Keep this one authored district surface in sync with
        // KentridgeDistrictTerraceCatalogue rather than pretending the macro profile is the final
        // ground at that sidewalk column. The focused regression evaluates the real terrace program
        // at the captured coordinate so drift here fails behaviorally instead of becoming a float.
        private const int WorkingYardXDm = 1490;
        private const int WorkingYardZDm = 570;
        private const int WorkingYardWidthDm = 260;
        private const int WorkingYardDepthDm = 250;
        private const int WorkingYardAnchorXDm = 1530;
        private const int WorkingYardAnchorZDm = 700;
        private const int WorkingYardShoulderDm = 54;
        private const int DistrictShoulderStepCount = 6;

        private readonly struct StreetPropPlacement
        {
            public readonly StreetPropKind Kind;
            public readonly int XDm;
            public readonly int ZDm;
            public readonly byte Orientation;

            public StreetPropPlacement(StreetPropKind kind, int xDm, int zDm,
                                       byte orientation = 0)
            {
                Kind = kind;
                XDm = xDm;
                ZDm = zDm;
                Orientation = orientation;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            List<StreetPropPlacement> placements = BuildPlacements();
            if (placements.Count != ExpectedPlacementCount)
                throw new InvalidOperationException(
                    "Kentridge streetscape produced an unexpected placement count: "
                    + placements.Count);

            int[][] programs =
            {
                LampProgram(settings),
                BenchProgram(settings),
                PlanterProgram(settings),
            };

            int programLength = 0;
            for (int i = 0; i < programs.Length; i++) programLength += programs[i].Length;

            var byKind = new List<StreetPropPlacement>[DefinitionCount];
            for (int i = 0; i < DefinitionCount; i++) byKind[i] = new List<StreetPropPlacement>();
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
                StreetPropKind kind = (StreetPropKind)id;
                int[] program = programs[id];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[id] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-street-" + KindName(kind)),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = Footprint(kind, scale),
                    MaxSlope = 32,
                    // Frontage paths are 60; public/private dressing occupies 80. Keep all small
                    // furniture at the same level so structures remain authoritative at 100+.
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
                    MaxPrimitives = 8,
                };

                List<StreetPropPlacement> kindPlacements = byKind[id];
                for (int i = 0; i < kindPlacements.Count; i++)
                {
                    StreetPropPlacement placement = kindPlacements[i];
                    int surfaceY = SurfaceYForPlacement(placement, seed, scale);
                    catalogue.ExplicitPlacements[placementOffset + i] = new ExplicitPlacement
                    {
                        Position = new int3(
                            placement.XDm * scale,
                            surfaceY,
                            placement.ZDm * scale),
                        Orientation = placement.Orientation,
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
                    "Kentridge street dressing catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static int SurfaceYForPlacement(StreetPropPlacement placement, uint seed, int scale)
        {
            if (TryWorkingYardSurfaceYAtDm(placement.XDm, placement.ZDm, seed, scale,
                                           out int districtSurfaceY))
                return districtSurfaceY;

            return KentridgeVerticalProfile.SurfaceYAtDm(
                placement.XDm, placement.ZDm, seed, scale);
        }

        private static bool TryWorkingYardSurfaceYAtDm(
            int xDm, int zDm, uint seed, int scale, out int surfaceY)
        {
            int minX = WorkingYardXDm - WorkingYardShoulderDm;
            int maxX = WorkingYardXDm + WorkingYardWidthDm + WorkingYardShoulderDm;
            int minZ = WorkingYardZDm - WorkingYardShoulderDm;
            int maxZ = WorkingYardZDm + WorkingYardDepthDm + WorkingYardShoulderDm;
            if (xDm < minX || xDm >= maxX || zDm < minZ || zDm >= maxZ)
            {
                surfaceY = 0;
                return false;
            }

            int coreY = KentridgeVerticalProfile.SurfaceYAtDm(
                WorkingYardAnchorXDm, WorkingYardAnchorZDm, seed, scale);
            int shoulder = WorkingYardShoulderDm * scale;

            if (zDm < WorkingYardZDm)
            {
                int edgeY = TerrainQuery.HeightAt(
                    (WorkingYardXDm + WorkingYardWidthDm / 2) * scale,
                    (WorkingYardZDm - WorkingYardShoulderDm) * scale,
                    seed);
                surfaceY = ShoulderSurfaceY(
                    (zDm - minZ) * scale, shoulder, edgeY, coreY,
                    outerAtNegativeAxis: true);
                return true;
            }

            if (zDm >= WorkingYardZDm + WorkingYardDepthDm)
            {
                int edgeY = TerrainQuery.HeightAt(
                    (WorkingYardXDm + WorkingYardWidthDm / 2) * scale,
                    (WorkingYardZDm + WorkingYardDepthDm + WorkingYardShoulderDm) * scale,
                    seed);
                surfaceY = ShoulderSurfaceY(
                    (zDm - (WorkingYardZDm + WorkingYardDepthDm)) * scale,
                    shoulder, edgeY, coreY, outerAtNegativeAxis: false);
                return true;
            }

            if (xDm < WorkingYardXDm)
            {
                int edgeY = TerrainQuery.HeightAt(
                    (WorkingYardXDm - WorkingYardShoulderDm) * scale,
                    (WorkingYardZDm + WorkingYardDepthDm / 2) * scale,
                    seed);
                surfaceY = ShoulderSurfaceY(
                    (xDm - minX) * scale, shoulder, edgeY, coreY,
                    outerAtNegativeAxis: true);
                return true;
            }

            if (xDm >= WorkingYardXDm + WorkingYardWidthDm)
            {
                int edgeY = TerrainQuery.HeightAt(
                    (WorkingYardXDm + WorkingYardWidthDm + WorkingYardShoulderDm) * scale,
                    (WorkingYardZDm + WorkingYardDepthDm / 2) * scale,
                    seed);
                surfaceY = ShoulderSurfaceY(
                    (xDm - (WorkingYardXDm + WorkingYardWidthDm)) * scale,
                    shoulder, edgeY, coreY, outerAtNegativeAxis: false);
                return true;
            }

            surfaceY = coreY;
            return true;
        }

        private static int ShoulderSurfaceY(
            int offset, int axisLength, int edgeY, int coreY, bool outerAtNegativeAxis)
        {
            for (int step = 0; step < DistrictShoulderStepCount; step++)
            {
                int start = axisLength * step / DistrictShoulderStepCount;
                int end = axisLength * (step + 1) / DistrictShoulderStepCount;
                int sliceStart = outerAtNegativeAxis ? start : axisLength - end;
                int sliceEnd = outerAtNegativeAxis ? end : axisLength - start;
                if (offset < sliceStart || offset >= sliceEnd) continue;

                return edgeY
                    + (coreY - edgeY) * (step + 1) / DistrictShoulderStepCount;
            }

            return coreY;
        }

        private static List<StreetPropPlacement> BuildPlacements()
        {
            var result = new List<StreetPropPlacement>(ExpectedPlacementCount);

            // Main-spine lamps sit just outside the 56 dm carriageway. The central market interval
            // already has eight square lanterns, so it remains intentionally un-doubled here.
            int westLampX = KentridgeTownPlanner.MainSpineXDm
                          - KentridgeTownPlanner.MainRoadWidthDm / 2 - 10;
            int eastLampX = KentridgeTownPlanner.MainSpineXDm
                          + KentridgeTownPlanner.MainRoadWidthDm / 2 + 2;
            int[] spineZ = { 95, 275, 700, 850, 1015 };
            for (int i = 0; i < spineZ.Length; i++)
            {
                result.Add(new StreetPropPlacement(StreetPropKind.Lamp, westLampX, spineZ[i]));
                result.Add(new StreetPropPlacement(StreetPropKind.Lamp, eastLampX, spineZ[i]));
            }

            // Market-street lamps frame the commercial row and east approach outside the plaza.
            int northMarketZ = KentridgeTownPlanner.MarketStreetZDm
                             - KentridgeTownPlanner.SecondaryRoadWidthDm / 2 - 15;
            int southMarketZ = KentridgeTownPlanner.MarketStreetZDm
                             + KentridgeTownPlanner.SecondaryRoadWidthDm / 2 + 5;
            int[] marketX = { 748, 905, 1030, 1350, 1530 };
            for (int i = 0; i < marketX.Length; i++)
            {
                result.Add(new StreetPropPlacement(StreetPropKind.Lamp, marketX[i], northMarketZ));
                result.Add(new StreetPropPlacement(StreetPropKind.Lamp, marketX[i], southMarketZ));
            }

            // Residential street gets a softer four-post rhythm, only on the south side where the
            // front setbacks leave enough room before the house envelopes.
            int residentialLampZ = KentridgeTownPlanner.ResidentialStreetZDm
                                  + KentridgeTownPlanner.ResidentialRoadWidthDm / 2 + 5;
            int[] residentialX = { 690, 865, 1060, 1280 };
            for (int i = 0; i < residentialX.Length; i++)
                result.Add(new StreetPropPlacement(
                    StreetPropKind.Lamp, residentialX[i], residentialLampZ));

            // Benches are pauses, not repeated furniture. Quarter-turn 1 faces the long axis along Z.
            result.Add(new StreetPropPlacement(StreetPropKind.Bench, 920, 120, 1));
            result.Add(new StreetPropPlacement(StreetPropKind.Bench, 1360, 120, 1));
            result.Add(new StreetPropPlacement(StreetPropKind.Bench, 760, 855, 0));

            // Low planters reinforce the shop row without competing with the market stalls.
            result.Add(new StreetPropPlacement(StreetPropKind.Planter, 755, 548));
            result.Add(new StreetPropPlacement(StreetPropKind.Planter, 895, 548));
            result.Add(new StreetPropPlacement(StreetPropKind.Planter, 1035, 548));

            return result;
        }

        private static int3 Footprint(StreetPropKind kind, int scale)
        {
            switch (kind)
            {
                case StreetPropKind.Lamp: return new int3(9 * scale, 44 * scale, 9 * scale);
                // Square X/Z bounds make both authored bench quarter-turns footprint-safe. The
                // actual bench remains a narrow 28x8 dm mesh within this conservative envelope.
                case StreetPropKind.Bench: return new int3(28 * scale, 18 * scale, 28 * scale);
                case StreetPropKind.Planter: return new int3(14 * scale, 12 * scale, 14 * scale);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static string KindName(StreetPropKind kind)
        {
            switch (kind)
            {
                case StreetPropKind.Lamp: return "lamp";
                case StreetPropKind.Bench: return "bench";
                case StreetPropKind.Planter: return "planter";
                default: return "unknown";
            }
        }

        private static int[] LampProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte warm = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte slate = settings.Materials.Resolve(MaterialRole.Slate);
            var b = new ProgramBuilder();

            // The base is the visible ground-contact owner. Reconstruct it exactly so the default
            // rounded stone presentation cannot retract its lower surface and make a correctly
            // adjacent authored lamp read as floating above the terrace.
            b.Cylinder(4 * s, 0, 4 * s, 3 * s, 4 * s, 1, stone, SurfaceStyles.Planar);
            // Dark stone is Smooth in the Showcase palette. The 3x3 pole is a deliberately thin
            // architectural support, so keep its occupancy/material but reconstruct it exactly;
            // otherwise the smoothed support can collapse while the larger lantern head remains.
            b.Box(3 * s, 3 * s, 3 * s, 3 * s, 29 * s, 3 * s,
                  dark, SurfaceStyles.Planar);
            b.Box(1 * s, 31 * s, 1 * s, 7 * s, 7 * s, 7 * s, warm);
            b.Prism(0, 38 * s, 0, 9 * s, 5 * s, 9 * s,
                    PrismProfile.Gable, slate);
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

        private static int[] PlanterProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte moss = settings.Materials.Resolve(MaterialRole.Moss);
            var b = new ProgramBuilder();

            b.Box(0, 0, 0, 14 * s, 3 * s, 14 * s, dark);
            b.Box(1 * s, 3 * s, 1 * s, 12 * s, 5 * s, 12 * s, stone);
            b.Box(3 * s, 8 * s, 3 * s, 8 * s, 2 * s, 8 * s, moss);
            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            ushort surfaceStyle = 0) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, surfaceStyle, 0, (int)PrimitiveMode.Fill);

            public void Cylinder(int x, int y, int z, int radius, int height,
                                 byte axis, byte material, ushort surfaceStyle = 0) =>
                Op(ShapeOp.EmitCylinder, x, y, z, radius, height, axis,
                   material, surfaceStyle, 0, (int)PrimitiveMode.Fill);

            public void Prism(int x, int y, int z, int sx, int sy, int sz,
                              PrismProfile profile, byte material) =>
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                   (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);

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
