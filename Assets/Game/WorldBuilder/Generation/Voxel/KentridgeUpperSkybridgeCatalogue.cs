using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Realises the Upper Ward court-to-court crossing as a light, open upper street. The bridge deck
    /// begins at the authored Upper Landing shelf elevation, preserving the semantic four-metre road
    /// clearance below. There are deliberately no road piers and no opaque roof: sparse timber frames
    /// make the over/under moment legible without turning the main ascent into a tunnel.
    /// </summary>
    public static class KentridgeUpperSkybridgeCatalogue
    {
        public const byte BridgePrecedence = 93;
        public const int DeckThicknessDm = 3;
        public const int ThresholdRunDm = 12;
        public const int ParapetWidthDm = 2;
        public const int ParapetHeightDm = 4;
        public const int FramePostDm = 3;
        public const int FrameHeightDm = 24;
        public const int FramePitchDm = 34;

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeUpperSkybridgePlan plan = KentridgeUpperSkybridgePlanner.Build(seed);
            int s = settings.VoxelsPerDecimetre;
            int[] program = BridgeProgram(plan, settings);

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

            int shelfY = KentridgeVerticalProfile.SurfaceYAtDm(
                plan.ShelfSampleDm.X,
                plan.ShelfSampleDm.Y,
                seed,
                s);
            int heightDm = DeckThicknessDm + FrameHeightDm + FramePostDm;

            catalogue.Definitions[0] = new FeatureDefinition
            {
                Name = new FixedString64Bytes("kentridge-upper-court-skybridge"),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(
                    plan.LengthDm * s,
                    heightDm * s,
                    plan.DepthDm * s),
                MaxSlope = 32,
                Precedence = BridgePrecedence,
                ProgramOffset = 0,
                ProgramLength = program.Length,
                MaxPrimitives = 48,
            };

            catalogue.ExplicitPlacements[0] = new ExplicitPlacement
            {
                Position = new int3(
                    plan.WestXDm * s,
                    shelfY,
                    plan.SouthZDm * s),
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
                    "Kentridge upper skybridge catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static int[] BridgeProgram(
            KentridgeUpperSkybridgePlan plan,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int length = plan.LengthDm * s;
            int depth = plan.DepthDm * s;
            int threshold = ThresholdRunDm * s;
            int deckH = DeckThicknessDm * s;
            int parapetW = ParapetWidthDm * s;
            int parapetH = ParapetHeightDm * s;
            int post = FramePostDm * s;
            int frameH = FrameHeightDm * s;

            if (length <= threshold * 2)
                throw new InvalidOperationException(
                    "Kentridge upper skybridge cannot fit both court thresholds.");

            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            var b = new ProgramBuilder();

            // Three shallow 1 dm rises at each end lift court pedestrians onto the 3 dm bridge deck.
            AddThreshold(b, 0, threshold, depth, deckH, stone, ascending: true);
            AddThreshold(b, length - threshold, length, depth, deckH, stone, ascending: false);

            int centralStart = threshold;
            int centralEnd = length - threshold;
            b.Box(centralStart, 0, 0,
                centralEnd - centralStart, deckH, depth, stone);

            // Low parapets define a street edge while leaving the crossing transparent from below.
            b.Box(centralStart, deckH, 0,
                centralEnd - centralStart, parapetH, parapetW, dark);
            b.Box(centralStart, deckH, depth - parapetW,
                centralEnd - centralStart, parapetH, parapetW, dark);

            // Sparse paired posts and longitudinal headers create a memorable overhead frame without
            // a solid roof. Crossbeams at each frame make the upper street read as architecture in
            // oblique views while the road sightline remains open through the bays.
            int firstFrame = centralStart + 6 * s;
            int lastFrame = centralEnd - 6 * s;
            int pitch = FramePitchDm * s;
            for (int x = firstFrame; x <= lastFrame; x += pitch)
            {
                int px = math.min(x, centralEnd - post);
                b.Box(px, deckH, 0, post, frameH, post, timber);
                b.Box(px, deckH, depth - post, post, frameH, post, timber);
                b.Box(px, deckH + frameH - post, 0,
                    post, post, depth, timber);
            }

            b.Box(centralStart, deckH + frameH - post, 0,
                centralEnd - centralStart, post, post, timber);
            b.Box(centralStart, deckH + frameH - post, depth - post,
                centralEnd - centralStart, post, post, timber);

            return b.Finish();
        }

        private static void AddThreshold(
            ProgramBuilder b,
            int start,
            int end,
            int depth,
            int deckH,
            byte material,
            bool ascending)
        {
            int span = end - start;
            int thirds = 3;
            for (int i = 0; i < thirds; i++)
            {
                int x0 = start + span * i / thirds;
                int x1 = start + span * (i + 1) / thirds;
                int heightIndex = ascending ? i + 1 : thirds - i;
                int height = deckH * heightIndex / thirds;
                b.Box(x0, 0, 0,
                    math.max(1, x1 - x0),
                    math.max(1, height),
                    depth,
                    material);
            }
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
                    0, 0, (int)PrimitiveMode.Fill);
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
