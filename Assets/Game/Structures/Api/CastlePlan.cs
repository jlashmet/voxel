using Unity.Mathematics;

namespace Game.Structures.Api
{
    /// <summary>Game-owned dimensions for one castle. Every field is in voxels; one voxel is 10 cm.</summary>
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
    /// Deterministic castle landmark geometry owned by game content. Engine structure authoring
    /// consumes the resolved coordinates and dimensions; it does not define castle vocabulary.
    /// </summary>
    public static class CastleLayout
    {
        public const int TrapdoorHalfSize = 8;
        public const int ChapelBellTowerSize = 56;
        public const int ChapelBellTowerStairRadius = 16;
        public const int FrontGateWidth = 48;
        public const int FrontGateHeight = 60;
        public const int FrontGateDepth = 4;
        public const int LowerRiverDepth = 88;

        public static int3 TrapdoorCentre(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
            return new int3(plan.Centre.x, baseY, keepMinZ + plan.KeepHalfZ + 40);
        }

        public static int3 FrontGateMinimum(in CastlePlan plan)
        {
            int baseY = plan.Centre.y + plan.PlateauHeight;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;
            return new int3(plan.Centre.x - FrontGateWidth / 2, baseY + 1,
                            gateZ - plan.WallThickness + 2);
        }

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
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ + 60;
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
