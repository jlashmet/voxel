using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>Dimensions drawn for one castle. Every field is in voxels; one voxel is 10 cm.</summary>
    public struct CastlePlan
    {
        public int3 Centre;

        public int PlateauRadius;
        public int PlateauHeight;
        public int CliffDrop;

        public int BaileyHalfX, BaileyHalfZ;
        public int WallHeight, WallThickness;

        public int TowerRadius, TowerHeight;
        public int GateTowerRadius, GateTowerHeight;

        public int KeepHalfX, KeepHalfZ, KeepHeight;
        public int FloorHeight;
        public int Floors;

        public uint Seed;
    }

    /// <summary>
    /// Deterministic castle landmark geometry shared with API-only world-generation clients.
    /// Construction remains owned by Structures.Runtime.
    /// </summary>
    public static class CastleLayout
    {
        /// <summary>
        /// Temporary compatibility offset used by the historical keep recipe. Spatial planning
        /// owns the actual keep centre; projection applies this once so Runtime and presentation
        /// share the same legacy anchor until the keep recipe is fully local-coordinate based.
        /// </summary>
        public const int LegacyKeepCentreZOffset = 60;

        public const int TrapdoorHalfSize = 8;
        public const int ChapelBellTowerSize = 56;
        public const int ChapelBellTowerStairRadius = 16;
        public const int FrontGateWidth = 48;
        public const int FrontGateHeight = 60;
        public const int FrontGateDepth = 4;
        public const int PosternGateWidth = 24;
        public const int PosternGateHeight = 38;
        public const int PosternGateDepth = 4;
        public const int LowerRiverDepth = 88;

        public static int3 TrapdoorCentre(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + LegacyKeepCentreZOffset;
            return new int3(plan.Centre.x, baseY, keepMinZ + plan.KeepHalfZ + 40);
        }

        /// <summary>
        /// Compatibility minimum for the historical centred -Z gate. The authoritative gate basis
        /// now lives in CastleGateGeometryResolver so realization and interaction cannot drift.
        /// </summary>
        public static int3 FrontGateMinimum(in CastlePlan plan) =>
            CastleGateGeometryResolver.LegacyFront(in plan).Origin;

        public static int WaterfallStreamX(in CastlePlan plan) =>
            plan.Centre.x + plan.BaileyHalfX + plan.TowerRadius + 36;

        public static int LowerRiverZAt(in CastlePlan plan, int x)
        {
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            return gateZ - plan.WallThickness - 92
                 + (int)math.round(math.sin((x - plan.Centre.x) * 0.028f) * 8f
                                  + math.sin((x - plan.Centre.x) * 0.071f) * 3f);
        }

        public static int WaterfallLipZ(in CastlePlan plan)
        {
            int streamX = WaterfallStreamX(in plan);
            return LowerRiverZAt(in plan, streamX) + 68;
        }

        public static int3 ChapelBellTowerCentre(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinX = plan.Centre.x - plan.KeepHalfX;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + LegacyKeepCentreZOffset;
            int keepWidth = plan.KeepHalfX * 2;
            int keepDepth = plan.KeepHalfZ * 2;
            int chapelWidth = math.max(78, keepWidth / 3);
            int chapelDepth = math.max(96, keepDepth * 3 / 5);
            int chapelMinX = keepMinX - chapelWidth + 4;
            int chapelMinZ = keepMinZ + keepDepth - chapelDepth - 38;
            int towerMinX = chapelMinX + 8;
            int towerMinZ = chapelMinZ + chapelDepth - 6;
            return new int3(towerMinX + ChapelBellTowerSize / 2, baseY,
                            towerMinZ + ChapelBellTowerSize / 2);
        }
    }
}
