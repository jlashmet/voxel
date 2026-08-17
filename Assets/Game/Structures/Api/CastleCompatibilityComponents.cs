using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Shared architectural component policy derived from the historical castle plan. This keeps
    /// the compatibility preset as game-owned policy while curtain, keep, tower, gate, and
    /// battlement authorers consume the same reusable contracts as other structure archetypes.
    /// </summary>
    public struct CastleCompatibilityComponents
    {
        public StructureFootprintConfig KeepFoundation;
        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public StructureWallRunConfig KeepWallX;
        public StructureWallRunConfig KeepWallZ;
        public TowerConfig CornerTowers;
        public TowerConfig GateTowers;
        public OpeningConfig MainGate;
        public BattlementConfig CurtainBattlements;

        public bool IsWellFormed =>
            KeepFoundation.IsWellFormed &&
            CurtainWallX.IsWellFormed &&
            CurtainWallZ.IsWellFormed &&
            KeepWallX.IsWellFormed &&
            KeepWallZ.IsWellFormed &&
            CornerTowers.IsWellFormed &&
            GateTowers.IsWellFormed &&
            MainGate.IsWellFormed &&
            CurtainBattlements.IsWellFormed;
    }

    /// <summary>
    /// Legacy compatibility mapping retained temporarily while the redundant castle configuration
    /// projections are reconciled. The distinct name prevents this adapter from competing with the
    /// canonical <see cref="CastleCompatibilityPreset"/> consumed by the active castle path.
    /// </summary>
    public static class CastleCompatibilityComponentsPreset
    {
        public static CastleCompatibilityComponents FromPlan(in CastlePlan plan)
        {
            int keepWidth = plan.KeepHalfX * 2;
            int keepDepth = plan.KeepHalfZ * 2;

            return new CastleCompatibilityComponents
            {
                KeepFoundation = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-6, -6),
                        new int2(keepWidth + 12, keepDepth + 12)),
                    FoundationStyle = StructureFoundationStyle.Slab,
                    FoundationDepth = 30,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                CurtainWallX = Wall(
                    plan.BaileyHalfX * 2,
                    plan.WallHeight,
                    plan.WallThickness),
                CurtainWallZ = Wall(
                    plan.BaileyHalfZ * 2,
                    plan.WallHeight,
                    plan.WallThickness),
                KeepWallX = Wall(keepWidth, plan.KeepHeight, 8),
                KeepWallZ = Wall(keepDepth, plan.KeepHeight, 8),
                CornerTowers = Tower(
                    count: 4,
                    radius: plan.TowerRadius,
                    height: plan.TowerHeight),
                GateTowers = Tower(
                    count: 2,
                    radius: plan.GateTowerRadius,
                    height: plan.GateTowerHeight),
                MainGate = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Door,
                    Width = CastleLayout.FrontGateWidth,
                    Height = CastleLayout.FrontGateHeight,
                    BottomOffset = 1,
                    Spacing = 0,
                    StartMargin = 0,
                    EndMargin = 0,
                    FrameThickness = 0,
                    LintelThickness = 0,
                    WidthVariation = 0,
                    HeightVariation = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Opening,
                },
                CurtainBattlements = new BattlementConfig
                {
                    ParapetThickness = 8,
                    ParapetHeight = 1,
                    MerlonWidth = 26,
                    MerlonHeight = 20,
                    GapWidth = 18,
                    CornerMerlonWidth = 26,
                    MaterialRole = StructureMaterialRole.PrimaryWall,
                },
            };
        }

        private static StructureWallRunConfig Wall(int length, int height, int thickness) =>
            new StructureWallRunConfig
            {
                Length = length,
                Height = height,
                Thickness = thickness,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
                RepetitionSpacing = 90,
                RepetitionOffset = 40,
            };

        private static TowerConfig Tower(int count, int radius, int height) =>
            new TowerConfig
            {
                Shape = StructureTowerShape.Round,
                Placement = count == 4
                    ? StructureTowerPlacement.Corners
                    : StructureTowerPlacement.Explicit,
                TopStyle = StructureTowerTopStyle.Parapet,
                Radius = radius,
                Height = height,
                Count = count,
                Spacing = 0,
                OpeningsEnabled = true,
                Opening = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Window,
                    Width = 14,
                    Height = 24,
                    BottomOffset = 9,
                    Spacing = 0,
                    FrameThickness = 3,
                    LintelThickness = 0,
                    FrameMaterialRole = StructureMaterialRole.Trim,
                    FillMaterialRole = StructureMaterialRole.Glass,
                },
                WallMaterialRole = StructureMaterialRole.PrimaryWall,
                TrimMaterialRole = StructureMaterialRole.Trim,
            };
    }
}
