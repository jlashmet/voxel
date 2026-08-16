using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-only projection of a resolved castle layout into interaction and presentation
    /// coordinates. Structures owns semantic/spatial planning; this class owns only application
    /// concerns that must follow the geometry which was actually realized.
    /// </summary>
    internal static class ShowcaseCastleSpatialLayout
    {
        internal static Vector3 PrimaryGateInteractionPosition(
            in CastleSpatialLayoutProjection projection,
            float voxelSize)
        {
            float3 point = projection.PrimaryGate.InteractionPointVoxels;
            return new Vector3(point.x, point.y, point.z) * voxelSize;
        }

        internal static int3[] PrimaryGateLeafVoxels(
            in CastleSpatialLayoutProjection projection)
        {
            CastleGateGeometry geometry = projection.PrimaryGate;
            var voxels = new List<int3>(geometry.Width * geometry.Height * geometry.Depth);
            for (int d = 0; d < geometry.Depth; d++)
            for (int w = 0; w < geometry.Width; w++)
            for (int h = 0; h < geometry.Height; h++)
            {
                if (!geometry.ContainsArchVoxel(w, h)) continue;
                voxels.Add(geometry.WorldVoxel(w, h, d));
            }
            return voxels.ToArray();
        }

        internal static Vector3 TrapdoorInteractionPosition(
            in CastleSpatialLayoutProjection projection,
            float voxelSize)
        {
            int3 centre = projection.TrapdoorCentre;
            return new Vector3(centre.x + 0.5f, centre.y + 0.2f, centre.z + 0.5f)
                 * voxelSize;
        }

        internal static int3 TrapdoorCentre(in CastleSpatialLayoutProjection projection) =>
            projection.TrapdoorCentre;

        internal static void BuildPresentationLights(
            in CastlePlan dimensions,
            in CastleSpatialLayoutProjection projection,
            out Vector4[] lights,
            out Vector4[] colours)
        {
            CastlePlan plan = projection.KeepPlan;
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ
                         + CastleSpatialLayoutProjection.LegacyKeepCentreZOffset;
            int keepCentreZ = keepMinZ + plan.KeepHalfZ;
            int keepMaxX = plan.Centre.x + plan.KeepHalfX;
            int wingWidth = math.max(96, plan.KeepHalfX * 4 / 5);
            int wingDepth = math.max(80, plan.KeepHalfZ * 2 - 72);
            int wingCentreX = keepMaxX - 4 + wingWidth / 2;
            int wingCentreZ = keepMinZ + 24 + wingDepth / 2;
            int chapelWidth = math.max(78, plan.KeepHalfX * 2 / 3);
            int chapelDepth = math.max(96, plan.KeepHalfZ * 6 / 5);
            int chapelCentreX = plan.Centre.x - plan.KeepHalfX - chapelWidth / 2 + 4;
            int chapelCentreZ = keepMinZ + plan.KeepHalfZ * 2 - chapelDepth / 2 - 38;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;
            int trapZ = keepMinZ + plan.KeepHalfZ + 40;
            int caveZ = trapZ - 411;
            int3 bellTower = projection.ChapelBellTowerCentre;

            static Vector4 LightAt(int x, int y, int z, float radiusMetres) =>
                new(x * 0.1f, y * 0.1f, z * 0.1f, radiusMetres);

            lights = new[]
            {
                LightAt(plan.Centre.x - 45, baseY + 26, keepCentreZ - 28, 8.0f),
                LightAt(plan.Centre.x + 42, baseY + 26, keepCentreZ + 30, 8.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight + 17, keepCentreZ, 8.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17, keepCentreZ, 7.0f),
                LightAt(wingCentreX, baseY + 17, wingCentreZ, 7.5f),
                LightAt(wingCentreX, baseY + plan.FloorHeight + 17, wingCentreZ, 7.0f),
                LightAt(chapelCentreX - 18, baseY + 24, chapelCentreZ, 7.5f),
                LightAt(chapelCentreX + 22, baseY + 27, chapelCentreZ, 7.5f),
                LightAt(plan.Centre.x - 55, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(plan.Centre.x + 58, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(plan.Centre.x - 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(plan.Centre.x + 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(plan.Centre.x + 226, dungeonY + 16, trapZ, 8.0f),
                LightAt(plan.Centre.x - 226, dungeonY + 15, trapZ, 8.0f),
                LightAt(plan.Centre.x - 40, dungeonY + 9, caveZ - 15, 11.5f),
                LightAt(plan.Centre.x + 45, dungeonY + 11, caveZ + 24, 11.5f),
                LightAt(plan.Centre.x + 145, dungeonY + 12, caveZ + 25, 10.5f),
                LightAt(plan.Centre.x - 52, baseY + plan.FloorHeight + 16,
                        keepCentreZ + 27, 6.5f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17,
                        keepCentreZ - 42, 6.0f),
                LightAt(plan.Centre.x, baseY + plan.FloorHeight * 3 + 17,
                        keepCentreZ + 42, 6.0f),
                LightAt(bellTower.x, baseY + 17, bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + plan.FloorHeight * 2 + 17,
                        bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + plan.FloorHeight * 3 + 17,
                        bellTower.z, 5.0f),
            };

            var hallWarm = new Vector4(1.00f, 0.38f, 0.10f, 1.85f);
            var upperWarm = new Vector4(1.00f, 0.40f, 0.13f, 1.05f);
            var chapelWarm = new Vector4(1.00f, 0.42f, 0.14f, 1.15f);
            var cellarWarm = new Vector4(1.00f, 0.28f, 0.06f, 2.05f);
            var sideRoomWarm = new Vector4(1.00f, 0.34f, 0.09f, 1.05f);
            var caveWarm = new Vector4(1.00f, 0.27f, 0.06f, 2.35f);
            var caveBlue = new Vector4(0.10f, 0.58f, 1.00f, 2.05f);
            colours = new[]
            {
                hallWarm, hallWarm, upperWarm, upperWarm, hallWarm, upperWarm,
                chapelWarm, chapelWarm,
                cellarWarm, cellarWarm, cellarWarm, cellarWarm, sideRoomWarm, sideRoomWarm,
                caveWarm, caveWarm, caveBlue,
                upperWarm, upperWarm, upperWarm,
                chapelWarm, upperWarm, upperWarm,
            };

            if (lights.Length != colours.Length)
            {
                throw new InvalidOperationException(
                    $"Castle presentation light/colour count mismatch: {lights.Length}/{colours.Length}.");
            }
        }
    }
}
