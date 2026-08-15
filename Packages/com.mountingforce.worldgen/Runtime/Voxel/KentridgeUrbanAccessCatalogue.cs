using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Hard pedestrian circulation for Kentridge's inhabited block faces. Each route cuts a narrow
    /// stair slot through the terrace, lays an L-shaped lower contour walk in front of the embedded
    /// facades, and marks the deliberate court opening with a small gateway.
    /// </summary>
    public static class KentridgeUrbanAccessCatalogue
    {
        private const int WalkThicknessDm = 2;
        private const int StairClearanceDm = 26;
        private const int GateHeightDm = 30;
        private const int GateRoofHeightDm = 12;
        private const int GatePierDm = 4;
        private const int CheekWallWidthDm = 2;
        private const int CheekWallHeightDm = 8;

        private sealed class CompiledAccess
        {
            public FixedString64Bytes Name;
            public int3 Position;
            public int3 Footprint;
            public int[] Program;
            public int MaxPrimitives;
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeUrbanAccessPlan plan = KentridgeUrbanAccessPlanner.Build(seed);
            var builds = new List<CompiledAccess>(plan.Routes.Count);
            for (int i = 0; i < plan.Routes.Count; i++)
                builds.Add(Compile(plan.Routes[i], seed, settings));

            int programLength = 0;
            for (int i = 0; i < builds.Count; i++)
                programLength += builds[i].Program.Length;

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: builds.Count,
                rules: builds.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: builds.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < builds.Count; i++)
            {
                CompiledAccess build = builds[i];
                for (int p = 0; p < build.Program.Length; p++)
                    catalogue.Program[programOffset + p] = build.Program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = build.Name,
                    Kind = FeatureKind.Infrastructure,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = build.Footprint,
                    MaxSlope = 32,
                    // Anonymous fabric is 86, embedded dwellings 90, galleries 92, civic bridge 95.
                    // Access must open ordinary retaining fabric but stays below the major bridge and
                    // all gameplay structures.
                    Precedence = 94,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = 0,
                    AnchorCount = 0,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = build.Program.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = build.MaxPrimitives,
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

                programOffset += build.Program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge urban access catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static CompiledAccess Compile(
            KentridgeUrbanAccessRoute route,
            uint seed,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int walk = KentridgeUrbanAccessPlanner.ContourWalkWidthDm;
            int landing = KentridgeUrbanAccessPlanner.TopLandingDepthDm;

            int minXDm = route.SouthMinXDm
                       - (route.ReturnSide == KentridgeUrbanReturnSide.West ? walk : 0);
            int maxXDm = route.SouthMaxXDm
                       + (route.ReturnSide == KentridgeUrbanReturnSide.East ? walk : 0);
            int minZDm = route.SouthZDm - walk;
            int maxZDm = Math.Max(
                route.ReturnSouthZDm,
                route.SouthZDm + route.StairLengthDm + landing);

            int shelfY = KentridgeVerticalProfile.SurfaceYAtDm(
                route.ElevationSampleDm.X,
                route.ElevationSampleDm.Y,
                seed,
                s);
            int doorY = shelfY - route.DoorLevelBelowShelfDm * s;
            int worldBaseY = doorY - WalkThicknessDm * s;

            int rise = route.DoorLevelBelowShelfDm * s;
            int heightDm = Math.Max(
                route.DoorLevelBelowShelfDm + WalkThicknessDm + StairClearanceDm + 2,
                WalkThicknessDm + GateHeightDm + GateRoofHeightDm + 2);

            return new CompiledAccess
            {
                Name = new FixedString64Bytes("kentridge-access-" + route.Id),
                Position = new int3(minXDm * s, worldBaseY, minZDm * s),
                Footprint = new int3(
                    (maxXDm - minXDm) * s,
                    heightDm * s,
                    (maxZDm - minZDm) * s),
                Program = AccessProgram(route, minXDm, minZDm, rise, settings),
                MaxPrimitives = 16 + route.StairSteps * 3,
            };
        }

        private static int[] AccessProgram(
            KentridgeUrbanAccessRoute route,
            int originXDm,
            int originZDm,
            int rise,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int walkDm = KentridgeUrbanAccessPlanner.ContourWalkWidthDm;
            int walk = walkDm * s;
            int thickness = WalkThicknessDm * s;
            int landingDepth = KentridgeUrbanAccessPlanner.TopLandingDepthDm * s;
            int southLength = route.SouthLengthDm * s;
            int returnLength = route.ReturnLengthDm * s;
            int southX = (route.SouthMinXDm - originXDm) * s;
            int southZ = (route.SouthZDm - originZDm) * s;

            int returnX = route.ReturnSide == KentridgeUrbanReturnSide.West
                ? (route.ReturnXDm - walkDm - originXDm) * s
                : (route.ReturnXDm - originXDm) * s;
            int returnZ = (route.ReturnNorthZDm - originZDm) * s;

            int stairWidthDm = Math.Max(12, Math.Min(18, route.CourtWidthDm - 8));
            int stairWidth = stairWidthDm * s;
            int stairX = (route.CourtCentreXDm - stairWidthDm / 2 - originXDm) * s;
            int stairZ = (route.SouthZDm - originZDm) * s - 2 * s;
            int stairLength = route.StairLengthDm * s + 2 * s;
            int slotMargin = 4 * s;
            int slotX = stairX - slotMargin;
            int slotW = stairWidth + 2 * slotMargin;
            int slotZ = stairZ;
            int slotD = stairLength + landingDepth;

            byte paving = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte roof = route.District == DistrictKind.Civic || route.District == DistrictKind.Noble
                ? settings.Materials.Resolve(MaterialRole.Slate)
                : settings.Materials.Resolve(MaterialRole.RoofTile);
            var b = new ProgramBuilder();

            // Cut headroom first; later hard stair/walk primitives become the final exposed surfaces.
            b.Carve(
                slotX,
                0,
                slotZ,
                slotW,
                rise + StairClearanceDm * s,
                slotD);

            // One lower contour strip crosses the south facade and another turns the occupied corner.
            // Their top surface is exactly the generated buildings' semantic door level.
            b.Box(southX, 0, southZ - walk,
                southLength, thickness, walk, paving);
            b.Box(returnX, 0, returnZ,
                walk, thickness, returnLength, paving);

            // The court stair begins on the lower contour and reaches the authored shelf surface.
            int cheekW = CheekWallWidthDm * s;
            int cheekH = CheekWallHeightDm * s;
            for (int i = 0; i < route.StairSteps; i++)
            {
                int z0 = stairZ + stairLength * i / route.StairSteps;
                int z1 = stairZ + stairLength * (i + 1) / route.StairSteps;
                int depth = Math.Max(1, z1 - z0);
                int y = route.StairSteps <= 1
                    ? rise
                    : rise * i / (route.StairSteps - 1);

                b.Box(stairX, y, z0, stairWidth, thickness, depth, stone);
                b.Box(stairX - cheekW, y, z0,
                    cheekW, cheekH, depth, dark);
                b.Box(stairX + stairWidth, y, z0,
                    cheekW, cheekH, depth, dark);
            }

            int topZ = stairZ + stairLength;
            b.Box(stairX - cheekW, rise, topZ,
                stairWidth + 2 * cheekW, thickness, landingDepth, paving);

            // A compact open gateway makes the court gap legible from below without consuming it.
            int gateWidth = route.CourtWidthDm * s;
            int gateX = (route.CourtCentreXDm - route.CourtWidthDm / 2 - originXDm) * s;
            int gateZ = southZ - 3 * s;
            int pier = GatePierDm * s;
            int gateH = GateHeightDm * s;
            b.Box(gateX, thickness, gateZ,
                pier, gateH, 6 * s, dark);
            b.Box(gateX + gateWidth - pier, thickness, gateZ,
                pier, gateH, 6 * s, dark);
            b.Box(gateX, thickness + gateH - 4 * s, gateZ,
                gateWidth, 4 * s, 6 * s, dark);
            b.Prism(gateX - 2 * s, thickness + gateH, gateZ - 2 * s,
                gateWidth + 4 * s, GateRoofHeightDm * s, 10 * s,
                PrismProfile.Gable, roof);

            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material,
                PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                    material, 0, 0, (int)mode);
            }

            public void Carve(
                int x, int y, int z,
                int sx, int sy, int sz)
            {
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);
            }

            public void Prism(
                int x, int y, int z,
                int sx, int sy, int sz,
                PrismProfile profile,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                    (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);
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
