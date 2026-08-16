using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Voxel profile for already-planned inner-ward towers. Planning owns whether these towers
    /// exist, where they stand, and whether each has a roof; Runtime owns only the smaller
    /// secondary-ring geometry profile.
    /// </summary>
    internal static class CastleInnerWardTowerRealizer
    {
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            CastleTowerPlacementSpec[] towers)
        {
            if (towers == null || towers.Length == 0)
                return;

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int radius = math.max(18, plan.TowerRadius * 3 / 4);
            int height = math.max(plan.WallHeight + 30, plan.TowerHeight * 4 / 5);

            for (int i = 0; i < towers.Length; i++)
            {
                CastleTowerPlacementSpec tower = towers[i];
                CastleTowerRealizer.Build(
                    ref brush,
                    in plan,
                    new int3(
                        plan.Centre.x + tower.Centre.x,
                        baseY,
                        plan.Centre.z + tower.Centre.y),
                    radius,
                    height + math.max(0, tower.HeightVariation),
                    tower.HasRoof);
            }
        }

        /// <summary>
        /// Transitional compatibility overload for an in-flight pipeline snapshot that still
        /// carries only centres. The spatial pipeline is being migrated to the spec overload above;
        /// this preserves historical output until that caller lands and prevents a broken branch.
        /// </summary>
        internal static void BuildAll(
            ref VoxelBrush brush,
            in CastlePlan plan,
            int2[] localCentres)
        {
            if (localCentres == null || localCentres.Length == 0)
                return;

            var towers = new CastleTowerPlacementSpec[localCentres.Length];
            for (int i = 0; i < towers.Length; i++)
            {
                uint variation = CastleSeedPartition.Derive(
                    plan.Seed, CastleSeedDomain.Walls, (uint)(0x2A00 + i));
                towers[i] = new CastleTowerPlacementSpec
                {
                    Id = i,
                    Centre = localCentres[i],
                    Role = CastleTowerPlacementRole.Corner,
                    HeightVariation = 0,
                    HasRoof = (variation & 1u) != 0u,
                };
            }

            BuildAll(ref brush, in plan, towers);
        }
    }
}
