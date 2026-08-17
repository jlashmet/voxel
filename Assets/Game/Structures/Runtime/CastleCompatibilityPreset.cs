using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Compatibility bridge from the existing seeded <see cref="CastlePlan"/> to the shared
    /// structure-component contracts. The bridge intentionally preserves the legacy dimensions and
    /// material choices while castle authorers migrate one stage at a time.
    /// </summary>
    public static class CastleCompatibilityPreset
    {
        public static CastleComponentConfig FromPlan(in CastlePlan plan)
        {
            var curtainX = CurtainWall(plan.BaileyHalfX * 2, in plan);
            var curtainZ = CurtainWall(plan.BaileyHalfZ * 2, in plan);

            var towerDoor = new OpeningConfig
            {
                Kind = StructureOpeningKind.Door,
                Width = 14,
                Height = 30,
                BottomOffset = 2,
                FillMaterialRole = StructureMaterialRole.Opening,
            };

            return new CastleComponentConfig
            {
                BaileyFootprint = new StructureFootprintConfig
                {
                    Primary = new StructureFootprintRect(
                        new int2(-plan.BaileyHalfX, -plan.BaileyHalfZ),
                        new int2(plan.BaileyHalfX * 2, plan.BaileyHalfZ * 2)),
                    BasePlane = BasePlaneRule.LowestGround,
                    // The legacy site stage authors its plateau/cliff terrain explicitly. Keep the
                    // shared foundation disabled until that stage can be migrated without changing
                    // the historical castle silhouette.
                    FoundationStyle = StructureFoundationStyle.None,
                    FoundationDepth = 0,
                    FoundationMaterial = StructureMaterialRole.Foundation,
                },
                CurtainWallX = curtainX,
                CurtainWallZ = curtainZ,
                CornerTowers = new TowerConfig
                {
                    Shape = StructureTowerShape.Round,
                    Placement = StructureTowerPlacement.Corners,
                    TopStyle = StructureTowerTopStyle.Parapet,
                    Radius = plan.TowerRadius,
                    Height = plan.TowerHeight,
                    Count = 4,
                    Spacing = 0,
                    OpeningsEnabled = true,
                    Opening = towerDoor,
                    WallMaterialRole = StructureMaterialRole.PrimaryWall,
                    TrimMaterialRole = StructureMaterialRole.Detail,
                },
                MainGate = new OpeningConfig
                {
                    Kind = StructureOpeningKind.Arch,
                    Width = CastleLayout.FrontGateWidth,
                    Height = CastleLayout.FrontGateHeight,
                    BottomOffset = 1,
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
                Palette = LegacyPalette(),
            };
        }

        private static StructureWallRunConfig CurtainWall(int length, in CastlePlan plan)
        {
            var wall = new StructureWallRunConfig
            {
                Length = length,
                Height = plan.WallHeight,
                Thickness = plan.WallThickness,
                PrimaryMaterial = StructureMaterialRole.PrimaryWall,
                CornerBehavior = StructureWallCornerBehavior.Overlap,
                RepetitionSpacing = 0,
                RepetitionOffset = 0,
            };

            wall.MaterialBands.Add(new StructureWallMaterialBand(
                0,
                22,
                StructureMaterialRole.Detail));
            wall.MaterialBands.Add(new StructureWallMaterialBand(
                plan.WallHeight * 66 / 100,
                2,
                StructureMaterialRole.Detail));
            return wall;
        }

        private static StructureMaterialPalette LegacyPalette()
        {
            return new StructureMaterialPalette
            {
                Foundation = GameMaterialIds.DarkStone,
                PrimaryWall = GameMaterialIds.Stone,
                SecondaryWall = GameMaterialIds.Wood,
                Trim = GameMaterialIds.DarkStone,
                Roof = GameMaterialIds.Slate,
                Floor = GameMaterialIds.Wood,
                Column = GameMaterialIds.Stone,
                Accent = GameMaterialIds.Gold,
                Underground = GameMaterialIds.DarkStone,
                Opening = GameMaterialIds.Empty,
                Glass = GameMaterialIds.LitWindow,
                Detail = GameMaterialIds.DarkStone,
            };
        }
    }
}
