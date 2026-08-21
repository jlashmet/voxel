using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Gives the already-authored Market Square a distinct architectural ground plane. The terrain
    /// backend still owns grading/support; this thin hard layer simply turns the road-coloured plaza
    /// into a flush shared-space piazza. Streets remain topologically continuous because there is no
    /// curb or height break, and the central gameplay Well continues to win at final precedence.
    /// </summary>
    public static class KentridgeMarketPiazzaCatalogue
    {
        public const byte PiazzaPrecedence = 61;
        public const int SurfaceThicknessDm = 2;
        public const int BorderWidthDm = 5;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            SettlementPlan settlement = SettlementVoxelPlan.Resolve(seed, in settings);
            PlannedPlaza plaza = settlement.Plaza;
            int s = settings.VoxelsPerDecimetre;
            int width = plaza.SizeDm.X * s;
            int depth = plaza.SizeDm.Y * s;
            int thickness = SurfaceThicknessDm * s;
            int[] program = PiazzaProgram(width, depth, settings);

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: program.Length,
                materials: 0,
                explicitPlacements: 1,
                overrides: 0,
                allocator);

            for (int i = 0; i < program.Length; i++)
                catalogue.Program[i] = program[i];

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-market-piazza-hard-surface"),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(width, thickness, depth),
                MaxSlope = 32,
                Precedence = PiazzaPrecedence,
                ProgramOffset = 0,
                ProgramLength = program.Length,
                MaxPrimitives = 5,
            };

            int surfaceY = KentridgeVerticalProfile.SurfaceYAtDm(
                plaza.CentreDm.X,
                plaza.CentreDm.Y,
                seed,
                s);
            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(
                    (plaza.CentreDm.X - plaza.SizeDm.X / 2) * s,
                    surfaceY - thickness,
                    (plaza.CentreDm.Y - plaza.SizeDm.Y / 2) * s),
                Orientation = 0,
                OverrideOffset = 0,
                OverrideCount = 0,
            };

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = 0,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 32,
                ExplicitOffset = 0,
                ExplicitCount = 1,
            };

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge market piazza catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static int[] PiazzaProgram(
            int width,
            int depth,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int border = BorderWidthDm * s;
            int thickness = SurfaceThicknessDm * s;
            if (width <= border * 2 || depth <= border * 2)
                throw new InvalidOperationException(
                    "Kentridge market piazza is too small for its architectural border.");

            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            // Four flush dark perimeter bands bound the public room. The lighter stone centre is a
            // single shared surface: road movement still passes through it without a geometric curb.
            b.Box(0, 0, 0, width, thickness, border, dark);
            b.Box(0, 0, depth - border, width, thickness, border, dark);
            b.Box(0, 0, border, border, thickness, depth - border * 2, dark);
            b.Box(width - border, 0, border, border, thickness, depth - border * 2, dark);
            b.Box(border, 0, border,
                width - border * 2,
                thickness,
                depth - border * 2,
                stone);

            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox,
                    x, y, z,
                    sx, sy, sz,
                    material,
                    0,
                    0,
                    (int)PrimitiveMode.Fill);
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
