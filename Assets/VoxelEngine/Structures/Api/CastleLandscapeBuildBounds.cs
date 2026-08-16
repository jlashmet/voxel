using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Conservative world-voxel envelope for planned stage-8 castle landscape geometry.
    /// Min is inclusive and MaxExclusive is exclusive.
    /// </summary>
    public readonly struct CastleLandscapeBuildBounds
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;

        internal CastleLandscapeBuildBounds(int3 min, int3 maxExclusive)
        {
            Min = min;
            MaxExclusive = maxExclusive;
        }

        public bool Contains(int3 voxel) =>
            math.all(voxel >= Min) && math.all(voxel < MaxExclusive);
    }

    /// <summary>
    /// Resolves the actual planned landscape footprint instead of relying on a generic castle halo.
    /// Y remains conservative because Runtime chooses the final occupied surface by scanning terrain.
    /// </summary>
    public static class CastleLandscapeBuildBoundsResolver
    {
        public static CastleLandscapeBuildBounds Resolve(
            in CastlePlan plan,
            CastleLandscapePlan landscape)
        {
            if (!CastleLandscapePlanValidator.TryValidate(
                    landscape, out CastleLandscapePlanIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle landscape bounds require a valid plan: {issue}.");
            }

            CastleLandscapeDecorationSpec[] decorations = landscape.Decorations;
            int top = plan.Centre.y + plan.PlateauHeight;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int minZ = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            int maxZ = int.MinValue;

            for (int i = 0; i < decorations.Length; i++)
            {
                CastleLandscapeDecorationSpec decoration = decorations[i];
                int x = plan.Centre.x + decoration.Centre.x;
                int z = plan.Centre.z + decoration.Centre.y;
                bool approach = IsApproach(decoration.Kind);
                int minimumSurface = top - (approach ? 170 : 100);
                int maximumSurface = top + (approach ? 18 : 14);

                switch (decoration.Kind)
                {
                    case CastleLandscapeDecorationKind.PerimeterMossShrub:
                    case CastleLandscapeDecorationKind.PerimeterGrassShrub:
                    case CastleLandscapeDecorationKind.ApproachDarkStoneRock:
                    case CastleLandscapeDecorationKind.ApproachStoneRock:
                    case CastleLandscapeDecorationKind.ApproachMossScrub:
                        Include(
                            x - decoration.Radius,
                            minimumSurface + 1,
                            z - decoration.Radius,
                            x + decoration.Radius,
                            maximumSurface + decoration.Height,
                            z + decoration.Radius,
                            ref minX, ref minY, ref minZ,
                            ref maxX, ref maxY, ref maxZ);
                        break;

                    case CastleLandscapeDecorationKind.PerimeterStoneRubble:
                    case CastleLandscapeDecorationKind.PerimeterDarkStoneRubble:
                        Include(
                            x,
                            minimumSurface + 1,
                            z,
                            x + decoration.Size.x - 1,
                            maximumSurface + decoration.Size.y,
                            z + decoration.Size.z - 1,
                            ref minX, ref minY, ref minZ,
                            ref maxX, ref maxY, ref maxZ);
                        break;
                }
            }

            return new CastleLandscapeBuildBounds(
                new int3(minX, minY, minZ),
                new int3(maxX + 1, maxY + 1, maxZ + 1));
        }

        private static bool IsApproach(CastleLandscapeDecorationKind kind) =>
            kind == CastleLandscapeDecorationKind.ApproachDarkStoneRock ||
            kind == CastleLandscapeDecorationKind.ApproachStoneRock ||
            kind == CastleLandscapeDecorationKind.ApproachMossScrub;

        private static void Include(
            int localMinX,
            int localMinY,
            int localMinZ,
            int localMaxX,
            int localMaxY,
            int localMaxZ,
            ref int minX,
            ref int minY,
            ref int minZ,
            ref int maxX,
            ref int maxY,
            ref int maxZ)
        {
            minX = math.min(minX, localMinX);
            minY = math.min(minY, localMinY);
            minZ = math.min(minZ, localMinZ);
            maxX = math.max(maxX, localMaxX);
            maxY = math.max(maxY, localMaxY);
            maxZ = math.max(maxZ, localMaxZ);
        }
    }
}
