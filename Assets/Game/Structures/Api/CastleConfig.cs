using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Game-owned castle composition over the reusable structure component contracts. Fields that
    /// have not yet migrated from <see cref="CastlePlan"/> remain in LegacyPlan; migrated geometry
    /// is resolved back into that plan at the compatibility boundary so the existing authorers can
    /// be moved incrementally without introducing a second castle-generation path.
    /// </summary>
    public struct CastleConfig
    {
        public CastlePlan LegacyPlan;

        public StructureFootprintConfig KeepFoundation;
        public int KeepFoundationTopOffset;

        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public TowerConfig CornerTowers;
        public TowerConfig GateTowers;
        public OpeningConfig MainGate;
        public BattlementConfig CurtainBattlements;
        public BattlementConfig GatehouseBattlements;

        public bool IsWellFormed
        {
            get
            {
                if (!KeepFoundation.IsWellFormed || KeepFoundationTopOffset < 0)
                    return false;
                if (!CurtainWallX.IsWellFormed || !CurtainWallZ.IsWellFormed)
                    return false;
                if (CurtainWallX.Height != CurtainWallZ.Height ||
                    CurtainWallX.Thickness != CurtainWallZ.Thickness)
                    return false;
                if ((CurtainWallX.Length & 1) != 0 || (CurtainWallZ.Length & 1) != 0)
                    return false;
                if (!CornerTowers.IsWellFormed || CornerTowers.Shape != StructureTowerShape.Round ||
                    CornerTowers.Placement != StructureTowerPlacement.Corners || CornerTowers.Count != 4)
                    return false;
                if (!GateTowers.IsWellFormed || GateTowers.Shape != StructureTowerShape.Round ||
                    GateTowers.Count != 2)
                    return false;
                if (MainGate.Kind != StructureOpeningKind.Arch || !MainGate.IsWellFormed)
                    return false;
                if (!CurtainBattlements.IsWellFormed || !GatehouseBattlements.IsWellFormed)
                    return false;

                return LegacyPlan.PlateauRadius > 0 && LegacyPlan.PlateauHeight > 0 &&
                    LegacyPlan.CliffDrop > 0 && LegacyPlan.KeepHalfX > 0 && LegacyPlan.KeepHalfZ > 0 &&
                    LegacyPlan.KeepHeight > 0 && LegacyPlan.FloorHeight > 0 && LegacyPlan.Floors > 0;
            }
        }

        /// <summary>
        /// Resolves migrated shared dimensions into the legacy plan consumed by castle stages that
        /// have not yet accepted their shared component directly. This is a compatibility adapter,
        /// not a second source of castle dimensions.
        /// </summary>
        public CastlePlan ResolvePlan()
        {
            CastlePlan resolved = LegacyPlan;
            resolved.BaileyHalfX = CurtainWallX.Length / 2;
            resolved.BaileyHalfZ = CurtainWallZ.Length / 2;
            resolved.WallHeight = CurtainWallX.Height;
            resolved.WallThickness = CurtainWallX.Thickness;
            resolved.TowerRadius = CornerTowers.Radius;
            resolved.TowerHeight = CornerTowers.Height;
            resolved.GateTowerRadius = GateTowers.Radius;
            resolved.GateTowerHeight = GateTowers.Height;
            return resolved;
        }
    }

    /// <summary>Castle presets are ordinary config factories; compatibility reproduces CastlePlan.</summary>
    public static class CastlePresets
    {
        public static CastleConfig Compatibility(in CastlePlan plan)
        {
            var foundation = new StructureFootprintConfig
            {
                Primary = new StructureFootprintRect(
                    new int2(-plan.KeepHalfX - 6, -plan.KeepHalfZ + 54),
                    new int2(plan.KeepHalfX * 2 + 12, plan.KeepHalfZ * 2 + 12)),
                FoundationStyle = StructureFoundationStyle.Slab,
                FoundationDepth = 30,
                FoundationMaterial = StructureMaterialRole.Foundation,
            };

            var wallX = new StructureWallRunConfig
            {
                Length = plan.BaileyHalfX * 2,
                Height = plan.WallHeight,
                Thickness = plan.WallThickness,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
            };
            var wallZ = wallX;
            wallZ.Length = plan.BaileyHalfZ * 2;

            var cornerTowers = new TowerConfig
            {
                Shape = StructureTowerShape.Round,
                Placement = StructureTowerPlacement.Corners,
                TopStyle = StructureTowerTopStyle.Parapet,
                Radius = plan.TowerRadius,
                Height = plan.TowerHeight,
                Count = 4,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            var gateTowers = new TowerConfig
            {
                Shape = StructureTowerShape.Round,
                Placement = StructureTowerPlacement.Explicit,
                TopStyle = StructureTowerTopStyle.Parapet,
                Radius = plan.GateTowerRadius,
                Height = plan.GateTowerHeight,
                Count = 2,
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };

            return new CastleConfig
            {
                LegacyPlan = plan,
                KeepFoundation = foundation,
                KeepFoundationTopOffset = 4,
                CurtainWallX = wallX,
                CurtainWallZ = wallZ,
                CornerTowers = cornerTowers,
                GateTowers = gateTowers,
                MainGate = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = CastleLayout.FrontGateWidth,
                    Height = CastleLayout.FrontGateHeight,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                CurtainBattlements = new BattlementConfig
                {
                    ParapetThickness = 8,
                    ParapetHeight = 0,
                    MerlonWidth = 26,
                    MerlonHeight = 20,
                    GapWidth = 18,
                    CornerMerlonWidth = 0,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                },
                GatehouseBattlements = new BattlementConfig
                {
                    ParapetThickness = 8,
                    ParapetHeight = 0,
                    MerlonWidth = 18,
                    MerlonHeight = 12,
                    GapWidth = 18,
                    CornerMerlonWidth = 0,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                },
            };
        }
    }
}
