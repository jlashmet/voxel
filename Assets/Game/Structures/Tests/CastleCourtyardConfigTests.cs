using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleCourtyardConfigTests
    {
        [Test]
        public void CompatibilityPresetPreservesCourtyardWellAndThreeBuildingSlots()
        {
            CastlePlan plan = CastlePlanner.Plan(new int3(80, 30, -40), 0x5511u);
            CastleCourtyardConfig courtyard = CastleCourtyardPresets.Compatibility(in plan);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(courtyard.IsWellFormed);
                Assert.AreEqual(
                    new int2(-plan.BaileyHalfX + 40, -plan.BaileyHalfZ + 40),
                    courtyard.OpenSpace.Area.Min);
                Assert.AreEqual(
                    new int2(plan.BaileyHalfX * 2 - 80, plan.BaileyHalfZ * 2 - 80),
                    courtyard.OpenSpace.Area.Size);
                Assert.AreEqual(OpenSpaceSurfaceMode.Paved, courtyard.OpenSpace.SurfaceMode);
                Assert.AreEqual(2, courtyard.OpenSpace.SurfaceThickness);
                Assert.AreEqual(StructureMaterialRole.PrimaryWall,
                    courtyard.OpenSpace.SurfaceMaterialRole);
                Assert.AreEqual(82, courtyard.PrimarySurfacePercent);

                Assert.IsTrue(courtyard.Well.Enabled);
                Assert.AreEqual(new int2(-plan.BaileyHalfX / 2, plan.BaileyHalfZ / 3),
                    courtyard.Well.LocalCentre);
                Assert.AreEqual(16, courtyard.Well.OuterRadius);
                Assert.AreEqual(11, courtyard.Well.InnerRadius);
                Assert.AreEqual(12, courtyard.Well.WallHeight);
                Assert.AreEqual(60, courtyard.Well.ShaftDepth);
                Assert.AreEqual(10, courtyard.Well.WaterRadius);
                Assert.AreEqual(14, courtyard.Well.WaterDepth);

                Assert.IsTrue(courtyard.AuthorCompatibilityBuildings);
                Assert.AreEqual(3, courtyard.SecondaryBuildingSlots.Length);
            });

            for (int i = 0; i < courtyard.SecondaryBuildingSlots.Length; i++)
            {
                CastleCourtyardBuildingSlotConfig slot = courtyard.SecondaryBuildingSlots[i];
                int expectedX = -plan.BaileyHalfX + 60 + i * 150;
                int expectedZ = plan.BaileyHalfZ - 130;
                int3 expectedWorld = new(
                    plan.Centre.x + expectedX,
                    plan.Centre.y + plan.PlateauHeight,
                    plan.Centre.z + expectedZ);

                Assert.Multiple(() =>
                {
                    Assert.AreEqual(new int2(expectedX, expectedZ), slot.LocalOrigin);
                    Assert.AreEqual(StructureAttachmentKind.Extension, slot.Anchor.Kind);
                    Assert.AreEqual(Facing.South, slot.Anchor.Facing);
                    Assert.AreEqual(expectedWorld,
                        courtyard.ResolveSecondaryBuildingAnchor(in plan, i));
                });
            }
        }

        [Test]
        public void CourtyardCanExposeAnchorsWithoutAuthoringPlaceholderBuildings()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x9912u);
            CastleCourtyardConfig courtyard = CastleCourtyardPresets.Compatibility(in plan);

            courtyard.AuthorCompatibilityBuildings = false;
            courtyard.PrimarySurfacePercent = 100;
            courtyard.Well.Enabled = false;
            courtyard.OpenSpace.Area = new StructureFootprintRect(
                new int2(-90, -70),
                new int2(180, 140));
            courtyard.SecondaryBuildingSlots.Clear();
            courtyard.SecondaryBuildingSlots.Add(new CastleCourtyardBuildingSlotConfig
            {
                LocalOrigin = new int2(24, 36),
                Anchor = new AttachmentAnchorConfig
                {
                    Kind = StructureAttachmentKind.Extension,
                    LocalPosition = new int3(24, plan.PlateauHeight, 36),
                    Facing = Facing.East,
                    SnapToGround = false,
                },
            });

            Assert.Multiple(() =>
            {
                Assert.IsTrue(courtyard.IsWellFormed);
                Assert.IsFalse(courtyard.AuthorCompatibilityBuildings);
                Assert.IsFalse(courtyard.Well.Enabled);
                Assert.AreEqual(100, courtyard.PrimarySurfacePercent);
                Assert.AreEqual(new int2(180, 140), courtyard.OpenSpace.Area.Size);
                Assert.AreEqual(1, courtyard.SecondaryBuildingSlots.Length);
                Assert.AreEqual(plan.Centre + new int3(24, plan.PlateauHeight, 36),
                    courtyard.ResolveSecondaryBuildingAnchor(in plan, 0));
            });
        }

        [Test]
        public void ValidationRejectsInvalidSurfaceWellAndSlotSemantics()
        {
            CastlePlan plan = CastlePlanner.Plan(int3.zero, 0x7712u);
            CastleCourtyardConfig valid = CastleCourtyardPresets.Compatibility(in plan);

            CastleCourtyardConfig invalidSurface = valid;
            invalidSurface.PrimarySurfacePercent = 101;

            CastleCourtyardConfig invalidWell = valid;
            invalidWell.Well.WaterRadius = invalidWell.Well.InnerRadius + 1;

            CastleCourtyardConfig invalidSlot = valid;
            CastleCourtyardBuildingSlotConfig slot = invalidSlot.SecondaryBuildingSlots[0];
            slot.Anchor.Kind = StructureAttachmentKind.Road;
            invalidSlot.SecondaryBuildingSlots[0] = slot;

            Assert.Multiple(() =>
            {
                Assert.IsFalse(invalidSurface.IsWellFormed);
                Assert.IsFalse(invalidWell.IsWellFormed);
                Assert.IsFalse(invalidSlot.IsWellFormed);
                Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                    valid.ResolveSecondaryBuildingAnchor(in plan, valid.SecondaryBuildingSlots.Length));
            });
        }
    }
}
