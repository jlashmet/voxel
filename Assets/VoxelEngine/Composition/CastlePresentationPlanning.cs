using System;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Presentation-only castle landmarks derived from the same spatial projection used by
    /// realization and interaction. Structures.Runtime stays free of scene-light concerns while
    /// presentation no longer re-derives the temporary keep compatibility anchor.
    /// </summary>
    public readonly struct CastlePresentationLayout
    {
        public readonly Vector4[] Lights;
        public readonly Vector4[] LightColours;

        public CastlePresentationLayout(Vector4[] lights, Vector4[] lightColours)
        {
            Lights = lights ?? Array.Empty<Vector4>();
            LightColours = lightColours ?? Array.Empty<Vector4>();
        }
    }

    public static class CastlePresentationPlanning
    {
        public static CastlePresentationLayout Create(in CastleSpatialProjection projection)
        {
            CastlePlan keepPlan = projection.KeepPlan;
            int2 keepCentre = projection.KeepCentreWorld;
            int baseY = keepPlan.Centre.y + keepPlan.PlateauHeight;
            int keepMinZ = keepCentre.y - keepPlan.KeepHalfZ;
            int keepCentreZ = keepCentre.y;
            int keepMaxX = keepCentre.x + keepPlan.KeepHalfX;
            int wingWidth = math.max(96, keepPlan.KeepHalfX * 4 / 5);
            int wingDepth = math.max(80, keepPlan.KeepHalfZ * 2 - 72);
            int wingCentreX = keepMaxX - 4 + wingWidth / 2;
            int wingCentreZ = keepMinZ + 24 + wingDepth / 2;
            int chapelWidth = math.max(78, keepPlan.KeepHalfX * 2 / 3);
            int chapelDepth = math.max(96, keepPlan.KeepHalfZ * 6 / 5);
            int chapelCentreX = keepCentre.x - keepPlan.KeepHalfX - chapelWidth / 2 + 4;
            int chapelCentreZ = keepMinZ + keepPlan.KeepHalfZ * 2 - chapelDepth / 2 - 38;
            int cellarY = baseY - 46;
            int dungeonY = cellarY - 120;
            int trapZ = projection.TrapdoorCentre.z;
            int caveZ = trapZ - 411;
            int3 bellTower = projection.ChapelBellTowerCentre;

            static Vector4 LightAt(int x, int y, int z, float radiusMetres) =>
                new(x * 0.1f, y * 0.1f, z * 0.1f, radiusMetres);

            Vector4[] lights =
            {
                LightAt(keepCentre.x - 45, baseY + 26, keepCentreZ - 28, 8.0f),
                LightAt(keepCentre.x + 42, baseY + 26, keepCentreZ + 30, 8.0f),
                LightAt(keepCentre.x, baseY + keepPlan.FloorHeight + 17, keepCentreZ, 8.0f),
                LightAt(keepCentre.x, baseY + keepPlan.FloorHeight * 3 + 17, keepCentreZ, 7.0f),
                LightAt(wingCentreX, baseY + 17, wingCentreZ, 7.5f),
                LightAt(wingCentreX, baseY + keepPlan.FloorHeight + 17, wingCentreZ, 7.0f),
                LightAt(chapelCentreX - 18, baseY + 24, chapelCentreZ, 7.5f),
                LightAt(chapelCentreX + 22, baseY + 27, chapelCentreZ, 7.5f),
                LightAt(keepCentre.x - 55, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(keepCentre.x + 58, cellarY + 17, keepCentreZ, 7.0f),
                LightAt(keepCentre.x - 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(keepCentre.x + 55, dungeonY + 18, trapZ, 8.5f),
                LightAt(keepCentre.x + 226, dungeonY + 16, trapZ, 8.0f),
                LightAt(keepCentre.x - 226, dungeonY + 15, trapZ, 8.0f),
                LightAt(keepCentre.x - 40, dungeonY + 9, caveZ - 15, 11.5f),
                LightAt(keepCentre.x + 45, dungeonY + 11, caveZ + 24, 11.5f),
                LightAt(keepCentre.x + 145, dungeonY + 12, caveZ + 25, 10.5f),
                LightAt(keepCentre.x - 52, baseY + keepPlan.FloorHeight + 16,
                        keepCentreZ + 27, 6.5f),
                LightAt(keepCentre.x, baseY + keepPlan.FloorHeight * 3 + 17,
                        keepCentreZ - 42, 6.0f),
                LightAt(keepCentre.x, baseY + keepPlan.FloorHeight * 3 + 17,
                        keepCentreZ + 42, 6.0f),
                LightAt(bellTower.x, baseY + 17, bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + keepPlan.FloorHeight * 2 + 17,
                        bellTower.z, 5.5f),
                LightAt(bellTower.x, baseY + keepPlan.FloorHeight * 3 + 17,
                        bellTower.z, 5.0f),
            };

            var hallWarm = new Vector4(1.00f, 0.38f, 0.10f, 1.85f);
            var upperWarm = new Vector4(1.00f, 0.40f, 0.13f, 1.05f);
            var chapelWarm = new Vector4(1.00f, 0.42f, 0.14f, 1.15f);
            var cellarWarm = new Vector4(1.00f, 0.28f, 0.06f, 2.05f);
            var sideRoomWarm = new Vector4(1.00f, 0.34f, 0.09f, 1.05f);
            var caveWarm = new Vector4(1.00f, 0.27f, 0.06f, 2.35f);
            var caveBlue = new Vector4(0.10f, 0.58f, 1.00f, 2.05f);
            Vector4[] colours =
            {
                hallWarm, hallWarm, upperWarm, upperWarm, hallWarm, upperWarm,
                chapelWarm, chapelWarm,
                cellarWarm, cellarWarm, cellarWarm, cellarWarm, sideRoomWarm, sideRoomWarm,
                caveWarm, caveWarm, caveBlue,
                upperWarm, upperWarm, upperWarm,
                chapelWarm, upperWarm, upperWarm,
            };

            return new CastlePresentationLayout(lights, colours);
        }
    }
}
