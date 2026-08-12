using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Features.Emitters;
using VoxelEngine.Core.Terrain;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Coarse voxel adapter for secondary urban circulation. It deliberately understands orthogonal
    /// city connectors rather than one named route: horizontal contours and vertical stair streets
    /// resolve through the same terrain/profile contract, while a later circulation grammar may turn
    /// the coarse ramp into steps, landings, arcades, or bridge pieces.
    /// </summary>
    public static class KentridgeUrbanCirculationCatalogue
    {
        private const int FillDepthDm = 5;
        private const int SurfaceThicknessDm = 3;
        private const int BuriedFootingDm = 5;
        private const int ClearAboveDm = 24;

        private readonly struct ConnectorBuild
        {
            public readonly KentridgeUrbanConnector Connector;
            public readonly int3 Footprint;
            public readonly ExplicitPlacement Placement;
            public readonly int ExtentX;
            public readonly int ExtentZ;
            public readonly int HeightDelta;
            public readonly int SupportDepth;
            public readonly int ClearHeight;
            public readonly byte RampAxis;

            public ConnectorBuild(
                KentridgeUrbanConnector connector,
                int3 footprint,
                ExplicitPlacement placement,
                int extentX,
                int extentZ,
                int heightDelta,
                int supportDepth,
                int clearHeight,
                byte rampAxis)
            {
                Connector = connector;
                Footprint = footprint;
                Placement = placement;
                ExtentX = extentX;
                ExtentZ = extentZ;
                HeightDelta = heightDelta;
                SupportDepth = supportDepth;
                ClearHeight = clearHeight;
                RampAxis = rampAxis;
            }
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeUrbanCirculationPlan plan = KentridgeUrbanCirculation.Build(seed);
            int scale = settings.VoxelsPerDecimetre;
            var builds = new ConnectorBuild[plan.Connectors.Count];
            var programs = new int[plan.Connectors.Count][];
            int programLength = 0;

            for (int i = 0; i < plan.Connectors.Count; i++)
            {
                builds[i] = Resolve(plan.Connectors[i], seed, scale);
                programs[i] = Program(builds[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
                definitions: builds.Length,
                rules: builds.Length,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: builds.Length,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int i = 0; i < builds.Length; i++)
            {
                ConnectorBuild build = builds[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++)
                    catalogue.Program[programOffset + p] = program[p];

                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes(
                        "kentridge-urban-connector-" + build.Connector.Id),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = build.Footprint,
                    MaxSlope = 32,
                    Precedence = 23,
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
                    MaxPrimitives = 4,
                };

                catalogue.ExplicitPlacements[i] = build.Placement;
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
                    "Kentridge urban circulation catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static ConnectorBuild Resolve(
            KentridgeUrbanConnector connector,
            uint seed,
            int scale)
        {
            if (connector.LengthDm <= 0 || connector.WidthDm <= 0
                || (!connector.IsHorizontal && !connector.IsVertical))
                throw new InvalidOperationException(
                    "Kentridge urban connector must be a non-zero orthogonal segment: " + connector.Id);

            bool horizontal = connector.IsHorizontal;
            Int2 minPoint;
            Int2 maxPoint;
            int extentX;
            int extentZ;
            int originXDm;
            int originZDm;
            byte axis;

            if (horizontal)
            {
                minPoint = connector.StartDm.X <= connector.EndDm.X
                    ? connector.StartDm : connector.EndDm;
                maxPoint = connector.StartDm.X <= connector.EndDm.X
                    ? connector.EndDm : connector.StartDm;
                extentX = connector.LengthDm * scale + 1;
                extentZ = connector.WidthDm * scale;
                originXDm = minPoint.X;
                originZDm = minPoint.Y - connector.WidthDm / 2;
                axis = 0;
            }
            else
            {
                minPoint = connector.StartDm.Y <= connector.EndDm.Y
                    ? connector.StartDm : connector.EndDm;
                maxPoint = connector.StartDm.Y <= connector.EndDm.Y
                    ? connector.EndDm : connector.StartDm;
                extentX = connector.WidthDm * scale;
                extentZ = connector.LengthDm * scale + 1;
                originXDm = minPoint.X - connector.WidthDm / 2;
                originZDm = minPoint.Y;
                axis = 2;
            }

            int targetMin = KentridgeVerticalProfile.SurfaceYAtDm(
                minPoint.X, minPoint.Y, seed, scale);
            int targetMax = KentridgeVerticalProfile.SurfaceYAtDm(
                maxPoint.X, maxPoint.Y, seed, scale);
            int lowTarget = Math.Min(targetMin, targetMax);
            int delta = Math.Abs(targetMax - targetMin);
            if (targetMin > targetMax)
                axis = (byte)(axis | BoxEmitter.ReverseRampBit);

            Int2 middle = new Int2(
                (minPoint.X + maxPoint.X) / 2,
                (minPoint.Y + maxPoint.Y) / 2);
            int naturalMinEnd = TerrainSampler.HeightAt(
                minPoint.X * scale, minPoint.Y * scale, seed);
            int naturalMid = TerrainSampler.HeightAt(
                middle.X * scale, middle.Y * scale, seed);
            int naturalMaxEnd = TerrainSampler.HeightAt(
                maxPoint.X * scale, maxPoint.Y * scale, seed);
            int minNatural = Math.Min(naturalMinEnd, Math.Min(naturalMid, naturalMaxEnd));
            int maxNatural = Math.Max(naturalMinEnd, Math.Max(naturalMid, naturalMaxEnd));

            int fillHeight = (FillDepthDm + SurfaceThicknessDm) * scale;
            int buried = BuriedFootingDm * scale;
            int supportDepth = Math.Max(0, lowTarget - minNatural) + buried;
            int clearHeight =
                ClearAboveDm * scale + delta + Math.Max(0, maxNatural - lowTarget);

            return new ConnectorBuild(
                connector,
                new int3(
                    extentX,
                    supportDepth + fillHeight + clearHeight,
                    extentZ),
                new ExplicitPlacement
                {
                    Position = new int3(
                        originXDm * scale,
                        lowTarget - fillHeight - supportDepth,
                        originZDm * scale),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                },
                extentX,
                extentZ,
                delta,
                supportDepth,
                clearHeight,
                axis);
        }

        private static int[] Program(
            ConnectorBuild build,
            VoxelWorldGenSettings settings)
        {
            int scale = settings.VoxelsPerDecimetre;
            int fillHeight = (FillDepthDm + SurfaceThicknessDm) * scale;
            byte surface = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte support = settings.Materials.Resolve(MaterialRole.FoundationStone);
            var b = new ProgramBuilder();

            b.Carve(
                0,
                build.SupportDepth + fillHeight,
                0,
                build.ExtentX,
                build.ClearHeight,
                build.ExtentZ);

            if (build.SupportDepth > 0)
                b.Box(
                    0, 0, 0,
                    build.ExtentX, build.SupportDepth, build.ExtentZ,
                    support);

            b.Box(
                0, build.SupportDepth, 0,
                build.ExtentX, fillHeight, build.ExtentZ,
                surface);

            if (build.HeightDelta > 0)
                b.Ramp(
                    0,
                    build.SupportDepth + fillHeight,
                    0,
                    build.ExtentX,
                    build.HeightDelta,
                    build.ExtentZ,
                    build.RampAxis,
                    surface);

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
                   material, (int)mode);
            }

            public void Carve(
                int x, int y, int z,
                int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public void Ramp(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte axis,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitRamp, x, y, z, sx, sy, sz,
                   axis, material, (int)PrimitiveMode.Fill);
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
