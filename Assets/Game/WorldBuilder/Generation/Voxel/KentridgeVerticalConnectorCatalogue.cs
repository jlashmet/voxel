using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Built retaining architecture for Kentridge's authored hillside.
    ///
    /// Smooth district landforms provide the earth mass and the directed town-surface stage already
    /// owns the continuous main-road climb. This stage supplies short arcaded retaining walls at
    /// important landings and a narrow summit campanile without overlaying a second stair route on
    /// the same carriageway. These are Infrastructure, not gameplay Structures, so stable building
    /// identity remains exactly the original seventeen roles.
    /// </summary>
    public static class KentridgeVerticalConnectorCatalogue
    {
        private const int RetainingDepthDm = 22;

        private sealed class CompiledBuild
        {
            public FixedString64Bytes Name;
            public int3 Position;
            public int3 Footprint;
            public int[] Program;
            public int Precedence;
            public int MaxPrimitives;
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            var builds = new List<CompiledBuild>(5);

            // The directed town-surface catalogue already realizes the main spine as one continuous,
            // supported climb. Do not add a second hard-stone stair ribbon inside that carriageway.

            // Walls deliberately stop short of the main carriageway. The gaps are not mistakes:
            // they expose the road penetration through each terrace and turn it into a clear
            // compositional ascent instead of hiding circulation behind one continuous wall.
            builds.Add(BuildRetainingWall(
                "market-retaining-west",
                xDm: 700, southEdgeZDm: 700, widthDm: 410, heightDm: 34, bays: 7,
                topSampleXDm: 1000, topSampleZDm: 520,
                seed, settings));
            builds.Add(BuildRetainingWall(
                "upper-retaining-west",
                xDm: 910, southEdgeZDm: 440, widthDm: 215, heightDm: 38, bays: 4,
                topSampleXDm: 1118, topSampleZDm: 340,
                seed, settings));
            builds.Add(BuildRetainingWall(
                "civic-retaining-west",
                xDm: 930, southEdgeZDm: 240, widthDm: 200, heightDm: 44, bays: 4,
                topSampleXDm: 1070, topSampleZDm: 150,
                seed, settings));
            builds.Add(BuildRetainingWall(
                "civic-retaining-east",
                xDm: 1210, southEdgeZDm: 240, widthDm: 165, heightDm: 44, bays: 3,
                topSampleXDm: 1300, topSampleZDm: 150,
                seed, settings));

            builds.Add(BuildCampanile(seed, settings));

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
                CompiledBuild build = builds[i];
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
                    // Roads/plaza are 20-25. Built hillside fabric sits on top of those smooth
                    // surfaces but below private dressing (80) and gameplay structures (100+).
                    Precedence = build.Precedence,
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
                    "Kentridge vertical infrastructure catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static CompiledBuild BuildRetainingWall(
            string name,
            int xDm, int southEdgeZDm, int widthDm, int heightDm, int bays,
            int topSampleXDm, int topSampleZDm,
            uint seed, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int topY = KentridgeVerticalProfile.SurfaceYAtDm(
                topSampleXDm, topSampleZDm, seed, s);
            int height = heightDm * s;
            int depth = RetainingDepthDm * s;
            int lowY = topY - height;

            return new CompiledBuild
            {
                Name = new FixedString64Bytes("kentridge-infrastructure-" + name),
                Position = new int3(
                    xDm * s,
                    lowY,
                    (southEdgeZDm - RetainingDepthDm) * s),
                Footprint = new int3(widthDm * s, height + 4 * s, depth),
                Program = RetainingWallProgram(widthDm, heightDm, bays, settings),
                Precedence = 35,
                MaxPrimitives = 6 + bays * 2,
            };
        }

        private static CompiledBuild BuildCampanile(uint seed, VoxelWorldGenSettings settings)
        {
            const int xDm = 922;
            const int zDm = 40;
            const int widthDm = 30;
            const int depthDm = 30;
            const int towerHeightDm = 178;
            const int roofHeightDm = 48;

            int s = settings.VoxelsPerDecimetre;
            int surfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                xDm + widthDm / 2, zDm + depthDm / 2, seed, s);

            return new CompiledBuild
            {
                Name = new FixedString64Bytes("kentridge-infrastructure-civic-campanile"),
                Position = new int3(xDm * s, surfaceY, zDm * s),
                Footprint = new int3(
                    widthDm * s,
                    (towerHeightDm + roofHeightDm + 4) * s,
                    depthDm * s),
                Program = CampanileProgram(settings),
                Precedence = 90,
                MaxPrimitives = 24,
            };
        }

        private static int[] RetainingWallProgram(int widthDm, int heightDm, int bays,
                                                  VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int width = widthDm * s;
            int height = heightDm * s;
            int depth = RetainingDepthDm * s;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            b.Box(0, 0, 8 * s, width, height, 10 * s, stone);

            int bayWidth = width / bays;
            int openingHeight = Math.Max(8 * s, height - 12 * s);
            for (int bay = 0; bay < bays; bay++)
            {
                int centre = bayWidth * bay + bayWidth / 2;
                int openingWidth = Math.Max(8 * s, Math.Min(28 * s, bayWidth - 9 * s));
                int x = Math.Max(3 * s, centre - openingWidth / 2);
                if (x + openingWidth > width - 3 * s)
                    openingWidth = width - 3 * s - x;

                // Deep rectangular arcade recess. The smooth earth shoulder behind the wall makes
                // this read as a usable vaulted retaining bay instead of a paper-thin facade.
                b.Carve(x, 4 * s, 0,
                        openingWidth, openingHeight, depth);
            }

            // Structural piers project downhill and survive between the recessed bays.
            for (int bay = 0; bay <= bays; bay++)
            {
                int x = Math.Min(width - 5 * s, Math.Max(0, bayWidth * bay - 2 * s));
                b.Box(x, 0, 3 * s, 5 * s, height, 15 * s, dark);
            }

            b.Box(0, Math.Max(0, height - 4 * s), 5 * s,
                  width, 4 * s, 15 * s, dark);
            return b.Finish();
        }

        private static int[] CampanileProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte glass = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);
            var b = new ProgramBuilder();

            b.Box(0, 0, 0, 30 * s, 10 * s, 30 * s, dark);
            b.Box(2 * s, 8 * s, 2 * s, 26 * s, 170 * s, 26 * s, stone);

            // Three strong horizontal stages keep the narrow tower legible at overview distance.
            b.Box(0, 52 * s, 0, 30 * s, 5 * s, 30 * s, dark);
            b.Box(0, 104 * s, 0, 30 * s, 5 * s, 30 * s, dark);
            b.Box(0, 154 * s, 0, 30 * s, 6 * s, 30 * s, dark);

            // Tall illuminated bell openings on all four sides of the top stage.
            b.Carve(9 * s, 128 * s, 0, 12 * s, 20 * s, 8 * s);
            b.Box(9 * s, 128 * s, 1 * s, 12 * s, 20 * s, 2 * s, glass);
            b.Carve(9 * s, 128 * s, 22 * s, 12 * s, 20 * s, 8 * s);
            b.Box(9 * s, 128 * s, 27 * s, 12 * s, 20 * s, 2 * s, glass);
            b.Carve(0, 128 * s, 9 * s, 8 * s, 20 * s, 12 * s);
            b.Box(1 * s, 128 * s, 9 * s, 2 * s, 20 * s, 12 * s, glass);
            b.Carve(22 * s, 128 * s, 9 * s, 8 * s, 20 * s, 12 * s);
            b.Box(27 * s, 128 * s, 9 * s, 2 * s, 20 * s, 12 * s, glass);

            b.Prism(0, 178 * s, 0,
                    30 * s, 48 * s, 30 * s, PrismProfile.Gable, roof);
            return b.Finish();
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
