using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Makes Kentridge's macro elevation change legible at player scale.
    ///
    /// The main spine remains a continuous ramp so carts and ordinary navigation have a simple
    /// route, but a narrow stone stair ribbon occupies one edge of each real ascent zone. Plateau
    /// sections deliberately have no steps. That alternation creates the visual language seen in
    /// hillside towns: climb, landing, climb, civic shelf, rather than one endless inclined plane.
    /// </summary>
    public static class KentridgeVerticalConnectorCatalogue
    {
        private const int StairWidthDm = 16;
        private const int TreadThicknessDm = 2;
        private const int SideInsetDm = 5;

        private readonly struct FlightBuild
        {
            public readonly FixedString64Bytes Name;
            public readonly int NorthZDm;
            public readonly int SouthZDm;
            public readonly int LowY;
            public readonly int Rise;
            public readonly int Steps;

            public FlightBuild(string name, int northZDm, int southZDm,
                               int lowY, int rise, int steps)
            {
                Name = new FixedString64Bytes(name);
                NorthZDm = northZDm;
                SouthZDm = southZDm;
                LowY = lowY;
                Rise = rise;
                Steps = steps;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            var flights = new List<FlightBuild>(4)
            {
                BuildFlight("south-rise", 760, 900, 10, seed, scale),
                BuildFlight("lower-market-rise", 620, 760, 14, seed, scale),
                BuildFlight("market-upper-rise", 300, 440, 17, seed, scale),
                BuildFlight("civic-rise", 160, 300, 15, seed, scale),
            };

            int programLength = 0;
            var programs = new int[flights.Count][];
            for (int i = 0; i < flights.Count; i++)
            {
                programs[i] = StairProgram(flights[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
                definitions: flights.Count,
                rules: flights.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: flights.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int xDm = KentridgeTownPlanner.MainSpineXDm
                    - KentridgeTownPlanner.MainRoadWidthDm / 2
                    + SideInsetDm;

            for (int i = 0; i < flights.Count; i++)
            {
                FlightBuild flight = flights[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                int length = (flight.SouthZDm - flight.NorthZDm) * scale;
                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = flight.Name,
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        StairWidthDm * scale,
                        flight.Rise + TreadThicknessDm * scale + 1,
                        length),
                    MaxSlope = 32,
                    // Above roads/plaza, below parcel retaining masses.
                    Precedence = 30,
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
                    MaxPrimitives = flight.Steps,
                };

                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(xDm * scale, flight.LowY, flight.NorthZDm * scale),
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
                    "Kentridge vertical connector catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static FlightBuild BuildFlight(string name, int northZ, int southZ, int steps,
                                               uint seed, int scale)
        {
            int xDm = KentridgeTownPlanner.MainSpineXDm;
            int highY = KentridgeVerticalProfile.SurfaceYAtDm(xDm, northZ, seed, scale);
            int lowY = KentridgeVerticalProfile.SurfaceYAtDm(xDm, southZ, seed, scale);
            int rise = highY - lowY;
            if (rise <= 0)
                throw new InvalidOperationException(
                    "Kentridge stair flight does not climb northward: " + name);

            return new FlightBuild(name, northZ, southZ, lowY, rise, steps);
        }

        private static int[] StairProgram(FlightBuild flight,
                                          VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int width = StairWidthDm * s;
            int thickness = TreadThicknessDm * s;
            int length = (flight.SouthZDm - flight.NorthZDm) * s;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var b = new ProgramBuilder();

            for (int i = 0; i < flight.Steps; i++)
            {
                int z0 = length * i / flight.Steps;
                int z1 = length * (i + 1) / flight.Steps;
                int depth = Math.Max(1, z1 - z0);
                int y = flight.Steps <= 1
                    ? flight.Rise
                    : flight.Rise * (flight.Steps - 1 - i) / (flight.Steps - 1);

                b.Box(0, y, z0, width, thickness, depth, stone);
            }

            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)PrimitiveMode.Fill);

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
