using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        /// <summary>
        /// Returns the player spawn above the castle's planned primary approach rather than a
        /// fixed world-axis position. The origin region queues the pending planned castle before
        /// the showcase asks for this value, while later respawns use the committed plan.
        /// </summary>
        public Vector3 CastleSpawnPosition()
        {
            PlannedCastleBuild planned = _hasCastlePlan ? _plannedCastle : _pendingPlannedCastle;
            CastlePlan dimensions = planned.Dimensions;
            CastleSpatialPlan spatial = planned.Spatial;
            if (spatial == null)
            {
                throw new InvalidOperationException(
                    "Castle spawn requires the pending or committed spatial castle plan.");
            }

            CastleSpatialProjection projection = planned.Projection;
            CastleBuildBounds bounds = CastleBuildBoundsResolver.Resolve(
                in dimensions, spatial);
            int2 column = ShowcaseCastleSpawnPlanner.PlanColumn(
                in dimensions, in projection, in bounds);
            int surface = SurfaceHeight(column.x, column.y);
            return new Vector3(
                column.x * VoxelSize,
                (surface + 40) * VoxelSize,
                column.y * VoxelSize);
        }

        /// <summary>World-space look target centred on the actually planned keep.</summary>
        public Vector3 CastleLookTargetPosition()
        {
            PlannedCastleBuild planned = _hasCastlePlan ? _plannedCastle : _pendingPlannedCastle;
            CastleSpatialPlan spatial = planned.Spatial;
            if (spatial == null)
            {
                throw new InvalidOperationException(
                    "Castle look target requires the pending or committed spatial castle plan.");
            }

            CastlePlan dimensions = planned.Dimensions;
            CastleSpatialProjection projection = planned.Projection;
            int2 keep = projection.KeepCentreWorld;
            int baseY = dimensions.Centre.y + dimensions.PlateauHeight;
            int targetY = baseY + dimensions.FloorHeight * 2;
            return new Vector3(
                keep.x * VoxelSize,
                targetY * VoxelSize,
                keep.y * VoxelSize);
        }
    }
}
