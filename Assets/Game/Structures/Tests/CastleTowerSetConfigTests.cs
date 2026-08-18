using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleTowerSetConfigTests
    {
        [Test]
        public void CompatibilityPresetPreservesFourRoundCornerTowers()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(40, 12, -30), 0x1234u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            CastleTowerSetConfig towers = CastleTowerSetPresets.Compatibility(in components);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(towers.IsWellFormed);
                Assert.AreEqual(StructureTowerPlacement.Corners, towers.Corners.Towers.Placement);
                Assert.AreEqual(StructureTowerShape.Round, towers.Corners.Towers.Shape);
                Assert.AreEqual(4, towers.Corners.Towers.Count);
                Assert.AreEqual(plan.TowerRadius, towers.Corners.Towers.Radius);
                Assert.AreEqual(plan.TowerHeight, towers.Corners.Towers.Height);
                Assert.AreEqual(0, towers.Corners.Taper);
                Assert.IsFalse(towers.IntermediateEnabled);
                Assert.IsTrue(towers.Corners.Crenellations.IsWellFormed);
            });
        }

        [Test]
        public void IntermediateSquareTowersExposeDimensionsTaperTopRoofAndOpenings()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x88u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            CastleTowerSetConfig towers = CastleTowerSetPresets.Compatibility(in components);

            towers.IntermediateEnabled = true;
            towers.Intermediate = new CastleTowerGroupConfig
            {
                Towers = new TowerConfig
                {
                    Shape = StructureTowerShape.Square,
                    Placement = StructureTowerPlacement.EvenlySpaced,
                    TopStyle = StructureTowerTopStyle.Roof,
                    Width = 40,
                    Depth = 36,
                    Height = 92,
                    Count = 3,
                    Spacing = 120,
                    OpeningsEnabled = true,
                    Opening = new OpeningConfig
                    {
                        Kind = StructureOpeningKind.Window,
                        Width = 8,
                        Height = 18,
                        BottomOffset = 10,
                        FillMaterialRole = StructureMaterialRole.Glass,
                    },
                    Roof = new RoofConfig
                    {
                        Style = RoofStyle.Hip,
                        RidgeAxis = RoofAxis.X,
                        PitchRise = 1,
                        PitchRun = 2,
                        Thickness = 1,
                        MaterialRole = StructureMaterialRole.Roof,
                    },
                    WallMaterialRole = StructureMaterialRole.PrimaryWall,
                    TrimMaterialRole = StructureMaterialRole.Trim,
                },
                Taper = 4,
                Crenellations = components.CurtainBattlements,
            };

            Assert.Multiple(() =>
            {
                Assert.IsTrue(towers.IsWellFormed);
                Assert.AreEqual(StructureTowerShape.Square, towers.Intermediate.Towers.Shape);
                Assert.AreEqual(40, towers.Intermediate.Towers.Width);
                Assert.AreEqual(36, towers.Intermediate.Towers.Depth);
                Assert.AreEqual(92, towers.Intermediate.Towers.Height);
                Assert.AreEqual(3, towers.Intermediate.Towers.Count);
                Assert.AreEqual(4, towers.Intermediate.Taper);
                Assert.AreEqual(StructureTowerTopStyle.Roof, towers.Intermediate.Towers.TopStyle);
                Assert.AreEqual(RoofStyle.Hip, towers.Intermediate.Towers.Roof.Style);
                Assert.AreEqual(8, towers.Intermediate.Towers.Opening.Width);
                Assert.IsTrue(towers.Intermediate.Crenellations.IsWellFormed);
            });
        }

        [Test]
        public void ExplicitTowerPlacementRequiresOnePositionPerTower()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x44u);
            CastleComponentConfig components = CastleCompatibilityComponents.Resolve(in plan);
            CastleTowerSetConfig towers = CastleTowerSetPresets.Compatibility(in components);
            CastleTowerGroupConfig group = towers.Corners;
            group.Towers.Placement = StructureTowerPlacement.Explicit;
            group.Towers.Count = 2;
            group.ExplicitPositions.Add(new int2(-100, 0));

            Assert.IsFalse(group.IsWellFormed);

            group.ExplicitPositions.Add(new int2(100, 0));
            Assert.IsTrue(group.IsWellFormed);
        }
    }
}
