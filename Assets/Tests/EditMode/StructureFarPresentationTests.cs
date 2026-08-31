using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class StructureFarPresentationTests
    {
        [Test]
        public void Resolve_IdenticalInputsProduceValueEquivalentDescriptor()
        {
            StructureIntent intent = Intent(StructureArchetype.Townhouse, new Int2(120, 340));
            StructureForm form = Form(intent);
            StructureSiteGeometry site = Site(new Int2(120, 340), FrontageDirection.East);
            StructureGeometryProfile profile = Profile();
            ArchitectureTheme theme = Theme();

            StructureFarPresentation first = StructureFarPresentationResolver.Resolve(
                "fixture-town", intent, form, site, profile, theme);
            StructureFarPresentation second = StructureFarPresentationResolver.Resolve(
                "fixture-town", intent, form, site, profile, theme);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.StructureKey, Is.EqualTo(first.StructureKey));
            Assert.That(second.Revision, Is.EqualTo(first.Revision));
        }

        [Test]
        public void Resolve_UsesCanonicalSiteFootprintAndFacing()
        {
            StructureIntent intent = Intent(StructureArchetype.Inn, new Int2(-850, 1900));
            StructureForm form = Form(intent);
            var site = new StructureSiteGeometry(
                new Int2(-840, 1910),
                new Int2(-710, 2015),
                new Int2(-710, 1960),
                publicEntranceHeightDm: 7,
                publicEntranceFacing: FrontageDirection.West);

            StructureFarPresentation result = StructureFarPresentationResolver.Resolve(
                "fixture-town", intent, form, site, Profile(), Theme());

            Assert.That(result.FootprintMinDm.X, Is.EqualTo(site.FootprintMinDm.X));
            Assert.That(result.FootprintMinDm.Y, Is.EqualTo(site.FootprintMinDm.Y));
            Assert.That(result.FootprintMaxDm.X, Is.EqualTo(site.FootprintMaxDm.X));
            Assert.That(result.FootprintMaxDm.Y, Is.EqualTo(site.FootprintMaxDm.Y));
            Assert.That(result.Facing, Is.EqualTo(site.PublicEntranceFacing));
        }

        [Test]
        public void Resolve_RelocationChangesRevisionButNotSemanticStructureIdentityOrVisibility()
        {
            StructureIntent nearIntent = Intent(StructureArchetype.Church, new Int2(0, 0));
            StructureIntent farIntent = Intent(StructureArchetype.Church, new Int2(120000, -90000));
            StructureFarPresentation near = StructureFarPresentationResolver.Resolve(
                "fixture-town",
                nearIntent,
                Form(nearIntent),
                Site(nearIntent.PositionDm, FrontageDirection.North),
                Profile(),
                Theme());
            StructureFarPresentation far = StructureFarPresentationResolver.Resolve(
                "fixture-town",
                farIntent,
                Form(farIntent),
                Site(farIntent.PositionDm, FrontageDirection.North),
                Profile(),
                Theme());

            Assert.That(near.StructureKey, Is.EqualTo(far.StructureKey),
                "semantic identity must not be derived from scene coordinates");
            Assert.That(near.Revision, Is.Not.EqualTo(far.Revision),
                "presentation revision must include changed placement facts");
            Assert.That(near.VisibilityClass, Is.EqualTo(StructureVisibilityClass.Landmark));
            Assert.That(far.VisibilityClass, Is.EqualTo(StructureVisibilityClass.Landmark));
        }

        [Test]
        public void Resolve_VisibilityOverrideIsSemanticPolicyAndReusableAcrossSettlements()
        {
            StructureIntent intent = Intent(StructureArchetype.Mansion, new Int2(800, 900));
            StructureForm form = Form(intent);
            StructureSiteGeometry site = Site(intent.PositionDm, FrontageDirection.South);

            StructureFarPresentation ordinaryTown = StructureFarPresentationResolver.Resolve(
                "fixture-town-a", intent, form, site, Profile(), Theme());
            StructureFarPresentation horizonTown = StructureFarPresentationResolver.Resolve(
                "fixture-town-b", intent, form, site, Profile(), Theme(),
                StructureVisibilityClass.HorizonLandmark);

            Assert.That(ordinaryTown.VisibilityClass,
                Is.EqualTo(StructureVisibilityClass.SettlementAnchor));
            Assert.That(horizonTown.VisibilityClass,
                Is.EqualTo(StructureVisibilityClass.HorizonLandmark));
            Assert.That(ordinaryTown.SettlementKey, Is.Not.EqualTo(horizonTown.SettlementKey));
            Assert.That(ordinaryTown.StructureKey, Is.Not.EqualTo(horizonTown.StructureKey));
        }

        private static StructureIntent Intent(StructureArchetype archetype, Int2 position) =>
            new StructureIntent(
                roleId: 17,
                styleId: "fixture-style",
                archetype: archetype,
                district: DistrictKind.Civic,
                positionDm: position,
                frontage: FrontageDirection.North,
                envelopeDm: new Int3(180, 160, 180));

        private static StructureForm Form(StructureIntent intent) =>
            new StructureForm(
                intent.RoleId,
                intent.Archetype,
                intent.District,
                StructureGenerationMode.Generated,
                FootprintForm.Rectangle,
                RoofForm.Gable,
                FrontageRhythm.ThreeBay,
                WindowTreatment.Glass,
                widthDm: 120,
                depthDm: 100,
                storeys: 2,
                doorOffsetDm: 0,
                upperOverhangDm: 2,
                roofHeightDm: 24,
                wingWidthDm: 0,
                wingDepthDm: 0,
                wingOnRight: false,
                chimneyOnRight: true);

        private static StructureSiteGeometry Site(Int2 min, FrontageDirection facing) =>
            new StructureSiteGeometry(
                min,
                new Int2(min.X + 120, min.Y + 100),
                new Int2(min.X + 60, min.Y),
                publicEntranceHeightDm: 7,
                publicEntranceFacing: facing);

        private static StructureGeometryProfile Profile() =>
            new StructureGeometryProfile(
                foundationCornerRadiusDm: 1,
                shellCornerRadiusDm: 3,
                openingCornerRadiusDm: 2,
                detailCornerRadiusDm: 2,
                foundationSurface: StructureSurfaceTreatment.Beveled,
                shellSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                openingSurface: StructureSurfaceTreatment.Rounded,
                detailSurface: StructureSurfaceTreatment.Beveled,
                roofSurface: StructureSurfaceTreatment.Smooth);

        private static ArchitectureTheme Theme() =>
            new ArchitectureTheme(
                id: "fixture-theme",
                foundation: MaterialRole.FoundationStone,
                wall: MaterialRole.Masonry,
                frame: MaterialRole.Timber,
                window: MaterialRole.Glass,
                roof: MaterialRole.RoofTile,
                accentStone: MaterialRole.DarkMasonry,
                foundationHeightDm: 7,
                wallThicknessDm: 4,
                floorHeightDm: 40,
                doorHeightDm: 24,
                windowBaseDm: 20,
                windowHeightDm: 12,
                beamWidthDm: 3,
                roofOverhangDm: 4,
                typicalRoofHeightDm: 24,
                grandRoofHeightDm: 32,
                upperStoreyOverhangDm: 5);
    }
}
