using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Realizes the already-planned stage-8 castle landscape payload. Placement, dimensions,
    /// material intent, and variation are frozen before Runtime begins mutation; this component
    /// resolves only terrain-relative surface Y and emits the corresponding voxel geometry.
    /// </summary>
    internal static class CastlePlannedLandscapeRealizer
    {
        internal static void Build(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleLandscapePlan landscape)
        {
            if (!CastleLandscapePlanValidator.TryValidate(
                    landscape, out CastleLandscapePlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle landscape plan is structurally invalid: {issue}.");
            }

            int top = plan.Centre.y + plan.PlateauHeight;
            CastleLandscapeDecorationSpec[] decorations = landscape.Decorations;
            for (int i = 0; i < decorations.Length; i++)
                Realize(ref brush, in plan, in decorations[i], top);
        }

        private static void Realize(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleLandscapeDecorationSpec decoration,
            int top)
        {
            int x = plan.Centre.x + decoration.Centre.x;
            int z = plan.Centre.z + decoration.Centre.y;
            bool approach = IsApproach(decoration.Kind);
            int surface = HighestSolid(
                ref brush,
                x,
                z,
                top + (approach ? 18 : 14),
                top - (approach ? 170 : 100));

            switch (decoration.Kind)
            {
                case CastleLandscapeDecorationKind.PerimeterMossShrub:
                    Cone(ref brush, x, surface, z, in decoration, Mat.Moss);
                    return;

                case CastleLandscapeDecorationKind.PerimeterGrassShrub:
                    Cone(ref brush, x, surface, z, in decoration, Mat.Grass);
                    return;

                case CastleLandscapeDecorationKind.PerimeterStoneRubble:
                    Rubble(ref brush, x, surface, z, in decoration, Mat.Stone);
                    return;

                case CastleLandscapeDecorationKind.PerimeterDarkStoneRubble:
                    Rubble(ref brush, x, surface, z, in decoration, Mat.DarkStone);
                    return;

                case CastleLandscapeDecorationKind.ApproachDarkStoneRock:
                    Cone(ref brush, x, surface, z, in decoration, Mat.DarkStone);
                    return;

                case CastleLandscapeDecorationKind.ApproachStoneRock:
                    Cone(ref brush, x, surface, z, in decoration, Mat.Stone);
                    return;

                case CastleLandscapeDecorationKind.ApproachMossScrub:
                    Cone(ref brush, x, surface, z, in decoration, Mat.Moss);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported castle landscape decoration kind {decoration.Kind}.");
            }
        }

        private static bool IsApproach(CastleLandscapeDecorationKind kind) =>
            kind == CastleLandscapeDecorationKind.ApproachDarkStoneRock ||
            kind == CastleLandscapeDecorationKind.ApproachStoneRock ||
            kind == CastleLandscapeDecorationKind.ApproachMossScrub;

        private static void Cone(
            ref VoxelBrush brush,
            int x,
            int surface,
            int z,
            in CastleLandscapeDecorationSpec decoration,
            byte material) =>
            brush.Cone(
                x,
                surface + 1,
                z,
                decoration.Radius,
                decoration.Height,
                material);

        private static void Rubble(
            ref VoxelBrush brush,
            int x,
            int surface,
            int z,
            in CastleLandscapeDecorationSpec decoration,
            byte material) =>
            brush.Box(
                new int3(x, surface + 1, z),
                decoration.Size,
                material);

        private static int HighestSolid(
            ref VoxelBrush brush,
            int x,
            int z,
            int fromY,
            int minY)
        {
            for (int y = fromY; y >= minY; y--)
                if (brush.IsSolid(x, y, z)) return y;
            return minY;
        }
    }
}
