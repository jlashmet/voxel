using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructureFarPresentationTests
    {
        [Test]
        public void IdenticalPlanningInputsProduceValueEquivalentDescriptor()
        {
            StructureIntent intent = Intent(new Int2(1200, -340));
            StructureForm form = Form();
            StructureSiteGeometry site = Site(new Int2(1200, -340), FrontageDirection.East);
            StructureGeometryProfile geometry = Geometry();
            ArchitectureTheme theme = Theme();

            StructureFarPresentation first = StructureFarPresentationResolver.Resolve(
                "settlement-alpha", intent, form, site, geometry, theme,
                StructureVisibilityClass.Landmark);
            StructureFarPresentation second = StructureFarPresentationResolver.Resolve(
                "settlement-alpha", intent, form, site, geometry, theme,
                StructureVisibilityClass.Landmark);

            Assert.AreEqual(first, second);
            Assert.AreEqual(first.Revision, second.Revision);
            Assert.AreEqual("settlement-alpha/role-41", first.StructureKey);
            Assert.AreEqual("settlement-alpha", first.ClusterKey);
            Assert.AreEqual(StructureArchetype.Church, first.Archetype);
            Assert.AreEqual(FootprintForm.RearWing, first.Footprint);
            Assert.AreEqual(RoofForm.SteepGable, first.Roof);
            Assert.AreEqual(3, first.Storeys);
            StringAssert.Contains("style-test/theme-test/materials-", first.ArchitectureFamilyKey);
        }

        [Test]
        public void DescriptorBoundsFacingAndHeightComeFromCanonicalArchitectureFacts()
        {
            StructureIntent intent = Intent(new Int2(300, 700));
            StructureForm form = Form();
            StructureSiteGeometry site = Site(new Int2(300, 700), FrontageDirection.North);
            ArchitectureTheme theme = Theme();

            StructureFarPresentation descriptor = StructureFarPresentationResolver.Resolve(
                "settlement-beta", intent, form, site, Geometry(), theme,
                StructureVisibilityClass.SettlementAnchor);

            Assert.AreEqual(site.FootprintMinDm.X, descriptor.FootprintMinDm.X);
            Assert.AreEqual(site.FootprintMinDm.Y, descriptor.FootprintMinDm.Y);
            Assert.AreEqual(site.FootprintMaxDm.X, descriptor.FootprintMaxDm.X);
            Assert.AreEqual(site.FootprintMaxDm.Y, descriptor.FootprintMaxDm.Y);
            Assert.AreEqual(site.PublicEntranceFacing, descriptor.Facing);
            Assert.AreEqual(
                theme.FoundationHeightDm + form.Storeys * theme.FloorHeightDm + form.RoofHeightDm,
                descriptor.HeightDm);
        }

        [Test]
        public void VisibilityClassIsExplicitSemanticPolicyAndDoesNotDependOnSceneCoordinates()
        {
            StructureFarPresentation nearOrigin = StructureFarPresentationResolver.Resolve(
                "settlement-gamma",
                Intent(new Int2(0, 0)),
                Form(),
                Site(new Int2(0, 0), FrontageDirection.South),
                Geometry(),
                Theme(),
                StructureVisibilityClass.HorizonLandmark);

            StructureFarPresentation farAway = StructureFarPresentationResolver.Resolve(
                "settlement-gamma",
                Intent(new Int2(900000, -700000)),
                Form(),
                Site(new Int2(900000, -700000), FrontageDirection.South),
                Geometry(),
                Theme(),
                StructureVisibilityClass.HorizonLandmark);

            Assert.AreEqual(StructureVisibilityClass.HorizonLandmark, nearOrigin.VisibilityClass);
            Assert.AreEqual(nearOrigin.VisibilityClass, farAway.VisibilityClass);
            Assert.AreNotEqual(nearOrigin.Revision, farAway.Revision,
                "Moving world identity must revise bounds/state, without changing semantic visibility policy.");
        }

        private static StructureIntent Intent(Int2 positionDm) =>
            new StructureIntent(
                41,
                "style-test",
                StructureArchetype.Church,
                DistrictKind.Civic,
                positionDm,
                FrontageDirection.South,
                new Int3(240, 190, 180));

        private static StructureForm Form() =>
            new StructureForm(
                41,
                StructureArchetype.Church,
                DistrictKind.Civic,
                StructureGenerationMode.Generated,
                FootprintForm.RearWing,
                RoofForm.SteepGable,
                FrontageRhythm.ThreeBay,
                WindowTreatment.Warm,
                180,
                140,
                3,
                0,
                4,
                72,
                45,
                55,
                true,
                false);

        private static StructureSiteGeometry Site(Int2 minDm, FrontageDirection facing) =>
            new StructureSiteGeometry(
                minDm,
                new Int2(minDm.X + 240, minDm.Y + 180),
                new Int2(minDm.X + 120, minDm.Y + 4),
                6,
                facing);

        private static StructureGeometryProfile Geometry() =>
            new StructureGeometryProfile(
                2,
                4,
                3,
                1,
                StructureSurfaceTreatment.Beveled,
                StructureSurfaceTreatment.ArchitecturalRounded,
                StructureSurfaceTreatment.Smooth,
                StructureSurfaceTreatment.Planar,
                StructureSurfaceTreatment.Smooth);

        private static ArchitectureTheme Theme() =>
            new ArchitectureTheme(
                "theme-test",
                MaterialRole.FoundationStone,
                MaterialRole.Masonry,
                MaterialRole.Timber,
                MaterialRole.WarmWindow,
                MaterialRole.Slate,
                MaterialRole.DarkMasonry,
                6,
                3,
                32,
                22,
                11,
                14,
                2,
                4,
                45,
                72,
                3);
    }
}
