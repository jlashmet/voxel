using System;
using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Freezes the already-validated primary gate-to-keep access route into the stage-8 landscape
    /// payload. Runtime receives only concrete road segments and never derives or pathfinds access.
    /// </summary>
    public static class CastleAccessPathCompletion
    {
        public static CastleSpatialPlan Attach(
            in CastlePlan plan,
            CastleSpatialPlan spatial)
        {
            if (spatial == null) throw new ArgumentNullException(nameof(spatial));
            if (spatial.KeepRequiresTerrainResolution)
                return spatial;
            if (spatial.Landscape == null)
                throw new InvalidOperationException(
                    "Castle access-path completion requires an attached landscape plan.");

            CastleAccessRoute route = CastleAccessRoute.Create(in plan, spatial);
            if (!CastleAccessRouteValidator.TryValidate(
                    in route,
                    spatial.OuterWardVertices,
                    spatial.InnerWardVertices,
                    out CastleAccessRouteIssue issue))
            {
                throw new InvalidOperationException(
                    $"Castle access-path completion received an invalid route: {issue}.");
            }

            var segments = new CastleLandscapeAccessPathSpec[route.WaypointCount - 1];
            int halfWidth = (int)math.ceil(CastleAccessRoute.CorridorHalfWidth);
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = new CastleLandscapeAccessPathSpec
                {
                    Id = i,
                    Start = route.Waypoint(i),
                    End = route.Waypoint(i + 1),
                    HalfWidth = halfWidth,
                };
            }

            spatial.Landscape.ReplaceAccessPaths(segments);
            return spatial;
        }
    }
}
