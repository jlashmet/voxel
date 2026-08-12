using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Hard pedestrian ledges outside Kentridge's downhill undercrofts. Every ledge includes a short
    /// side stair from the block's existing lower contour return, so the new level is physically tied
    /// into the circulation system instead of becoming inaccessible facade dressing.
    /// </summary>
    public static class KentridgeVerticalGalleryCatalogue
    {
        public const byte GalleryPrecedence = 88;
        public const int DeckThicknessDm = 2;
        public const int ParapetWidthDm = 2;
        public const int ParapetHeightDm = 5;
        private const int SideExtensionDm = 10;
        private const int StairWidthDm = 10;

        private sealed class CompiledGallery
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
            KentridgeVerticalGalleryPlan plan = KentridgeVerticalGalleryPlanner.Build(seed);
            var builds = new List<CompiledGallery>(plan.Routes.Count);
            int programLength = 0;
            for (int i = 0; i < plan.Routes.Count; i++)
            {
                CompiledGallery build = Compile(plan.Routes[i], seed, settings);
                builds.Add(build);
                programLength += build.Program.Length;
            }

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
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
                CompiledGallery build = builds[i];
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
                    Precedence = GalleryPrecedence,
                    ProgramOffset = programOffset,
                    ProgramLength = build.Program.Length,
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
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };
                programOffset += build.Program.Length;
            }

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge vertical gallery catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static CompiledGallery Compile(
            KentridgeVerticalGalleryRoute route,
            uint seed,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            bool west = route.ReturnSide == KentridgeUrbanReturnSide.West;
            int minXDm = route.MinXDm - (west ? SideExtensionDm : 0);
            int maxXDm = route.MaxXDm + (west ? 0 : SideExtensionDm);
            int shelfY = KentridgeVerticalProfile.SurfaceYAtDm(
                route.ElevationSampleDm.X,
                route.ElevationSampleDm.Y,
                seed,
                s);
            int lowerDoorY = shelfY - route.LowerDoorBelowShelfDm * s;
            int galleryFloorY = shelfY - route.GalleryFloorBelowShelfDm * s;
            int rise = galleryFloorY - lowerDoorY;
            int steps = Math.Max(4, (route.RiseDm + 1) / 2 + 1);

            int footprintHeight = rise
                + (DeckThicknessDm + ParapetHeightDm + 2) * s;

            return new CompiledGallery
            {
                Name = new FixedString64Bytes("kentridge-gallery-" + route.Id),
                Position = new int3(
                    minXDm * s,
                    lowerDoorY - DeckThicknessDm * s,
                    route.FrontZDm * s),
                Footprint = new int3(
                    (maxXDm - minXDm) * s,
                    footprintHeight,
                    KentridgeVerticalGalleryPlanner.GalleryDepthDm * s),
                Program = GalleryProgram(route, minXDm, rise, steps, settings),
                MaxPrimitives = 8 + steps,
            };
        }

        private static int[] GalleryProgram(
            KentridgeVerticalGalleryRoute route,
            int originXDm,
            int rise,
            int steps,
            VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            int deckT = DeckThicknessDm * s;
            int depth = KentridgeVerticalGalleryPlanner.GalleryDepthDm * s;
            int run = KentridgeVerticalGalleryPlanner.CornerStairRunDm * s;
            int stairW = StairWidthDm * s;
            int zoneX = (route.MinXDm - originXDm) * s;
            int zoneWidth = route.LengthDm * s;
            int totalWidth = zoneWidth + SideExtensionDm * s;
            int gapCentre = (route.GapCentreXDm - originXDm) * s;
            int gapWidth = route.GapWidthDm * s;
            int gapStart = gapCentre - gapWidth / 2;
            int gapEnd = gapStart + gapWidth;

            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte dark = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            var b = new ProgramBuilder();

            // Full gallery deck gives the open undercroft a usable second-level pedestrian frontage.
            b.Box(zoneX, rise, 0,
                zoneWidth, deckT, depth, stone);

            // Low downhill parapet remains open at the aligned gateway so the vertical axis stays
            // readable from below and later circulation can specialise the opening.
            int parapetZ = depth - ParapetWidthDm * s;
            int parapetY = rise + deckT;
            int parapetH = ParapetHeightDm * s;
            AddSpan(b, zoneX, Math.Max(zoneX, gapStart), parapetY,
                parapetZ, ParapetWidthDm * s, parapetH, dark);
            AddSpan(b, Math.Min(zoneX + zoneWidth, gapEnd), zoneX + zoneWidth,
                parapetY, parapetZ, ParapetWidthDm * s, parapetH, dark);

            // The existing access return arrives one side-extension outside the block at lower door
            // level. Rise from that exact corner into the first gallery bay over a compact 3 m run.
            bool west = route.ReturnSide == KentridgeUrbanReturnSide.West;
            int runStart = west ? 0 : totalWidth - run;
            for (int i = 0; i < steps; i++)
            {
                int x0;
                int x1;
                if (west)
                {
                    x0 = runStart + run * i / steps;
                    x1 = runStart + run * (i + 1) / steps;
                }
                else
                {
                    x0 = totalWidth - run * (i + 1) / steps;
                    x1 = totalWidth - run * i / steps;
                }

                int stepRise = steps <= 1
                    ? rise
                    : rise * i / (steps - 1);
                b.Box(x0, stepRise, 0,
                    Math.Max(1, x1 - x0), deckT, stairW, stone);
            }

            return b.Finish();
        }

        private static void AddSpan(
            ProgramBuilder b,
            int start,
            int end,
            int y,
            int z,
            int depth,
            int height,
            byte material)
        {
            int width = end - start;
            if (width <= 0) return;
            b.Box(start, y, z, width, height, depth, material);
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
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                    material, (int)PrimitiveMode.Fill);
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
