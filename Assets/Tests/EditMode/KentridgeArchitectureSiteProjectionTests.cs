using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeArchitectureSiteProjectionTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void GeneratedGeometryUsesFixedEnvelopeExactFrontageAndGuaranteedInterior()
        {
            var intent = new StructureIntent(
                77,
                KentridgeDefinition.Id,
                StructureArchetype.Shop,
                DistrictKind.Market,
                new Int2(100, 200),
                FrontageDirection.West,
                new Int3(124, 120, 124));
            var form = new StructureForm(
                77,
                StructureArchetype.Shop,
                DistrictKind.Market,
                StructureGenerationMode.Generated,
                FootprintForm.RearWing,
                RoofForm.GableWithLeanTo,
                FrontageRhythm.ThreeBay,
                WindowTreatment.Glass,
                94, 70, 2, -10,
                0, 20, 30, 26,
                false, true);

            StructureSiteGeometry geometry;
            Assert.IsTrue(StructureSiteGeometryResolver.TryResolve(
                intent, KentridgeDefinition.Theme, form, out geometry));

            Assert.AreEqual(100, geometry.FootprintMinDm.X);
            Assert.AreEqual(200, geometry.FootprintMinDm.Y);
            Assert.AreEqual(224, geometry.FootprintMaxDm.X);
            Assert.AreEqual(324, geometry.FootprintMaxDm.Y);

            // Local shop door centre is (52,10). West is quarter-turn 1 inside a 124x124
            // envelope: (123-10,52), then translated by the world origin.
            Assert.AreEqual(213, geometry.PublicEntranceDm.X);
            Assert.AreEqual(252, geometry.PublicEntranceDm.Y);
            Assert.AreEqual(7, geometry.PublicEntranceHeightDm);
            Assert.AreEqual(FrontageDirection.West, geometry.PublicEntranceFacing);

            StructureInteriorEnvelope interior;
            Assert.IsTrue(StructureSiteGeometryResolver.TryResolveInterior(
                intent, KentridgeDefinition.Theme, form, out interior));
            Assert.AreEqual(33, interior.HalfWidthDm);
            Assert.AreEqual(66, interior.DepthDm);
        }

        [Test]
        public void BespokeBuildingGeometryIsOwnedByArchitectureInsteadOfVoxel()
        {
            AssertBespoke(
                roleId: (int)KentridgeRole.Warehouse,
                archetype: StructureArchetype.Warehouse,
                envelope: 196,
                expectedDoorX: 94,
                expectedDoorZ: 18,
                expectedDoorHeight: 8,
                expectedHalfWidth: 74,
                expectedDepth: 137);
            AssertBespoke(
                roleId: (int)KentridgeRole.RadcliffeMansion,
                archetype: StructureArchetype.Mansion,
                envelope: 268,
                expectedDoorX: 131,
                expectedDoorZ: 26,
                expectedDoorHeight: 9,
                expectedHalfWidth: 100,
                expectedDepth: 183);
            AssertBespoke(
                roleId: (int)KentridgeRole.Church,
                archetype: StructureArchetype.Church,
                envelope: 164,
                expectedDoorX: 82,
                expectedDoorZ: 18,
                expectedDoorHeight: 8,
                expectedHalfWidth: 10,
                expectedDepth: 42);
        }

        [Test]
        public void WellInteractionAnchorIsNotMisrepresentedAsPublicEntrance()
        {
            var intent = new StructureIntent(
                (int)KentridgeRole.Well,
                KentridgeDefinition.Id,
                StructureArchetype.Well,
                DistrictKind.Civic,
                new Int2(100, 200),
                FrontageDirection.South,
                new Int3(56, 60, 56));
            StructureForm form = BespokeForm(intent);

            StructureSiteGeometry geometry;
            StructureInteriorEnvelope interior;
            Assert.IsFalse(StructureSiteGeometryResolver.TryResolve(
                intent, KentridgeDefinition.Theme, form, out geometry));
            Assert.IsFalse(StructureSiteGeometryResolver.TryResolveInterior(
                intent, KentridgeDefinition.Theme, form, out interior));
        }

        [Test]
        public void KentridgeProjectionExposesSixteenDoorAccessibleSitesAndExcludesWell()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var provider = new KentridgeArchitectureSiteProjectionProvider(plan);
            int projected = 0;
            int excluded = 0;

            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                SettlementSiteProjection projection;
                if (!provider.TryProject(site, out projection))
                {
                    excluded++;
                    Assert.AreEqual((int)KentridgeRole.Well, site.RoleId);
                    CutsceneStageEnvelope excludedEnvelope;
                    Assert.IsFalse(provider.TryGetCutsceneStageEnvelope(site, out excludedEnvelope));
                    continue;
                }

                projected++;
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.Interior));
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.PublicExit));
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.ConversationSpace));
                Assert.IsTrue(HasCapability(projection, SiteCapabilityKind.CutsceneStage));

                CutsceneStageEnvelope envelope;
                Assert.IsTrue(provider.TryGetCutsceneStageEnvelope(site, out envelope));
                Assert.Greater(envelope.InteriorHalfWidthDecimetres, 0);
                Assert.Greater(envelope.InteriorDepthDecimetres, 0);

                SiteArchetype expectedArchetype = site.RoleId == (int)KentridgeRole.Pub
                    ? SiteArchetype.Pub
                    : SiteArchetype.Unspecified;
                Assert.AreEqual(expectedArchetype, projection.Archetype);
            }

            Assert.AreEqual(16, projected);
            Assert.AreEqual(1, excluded);
        }

        [Test]
        public void ArchitectureProjectionMatchesExactVoxelEntranceAtDefaultScale()
        {
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            var projections = new KentridgeArchitectureSiteProjectionProvider(plan);
            var realization = new KentridgeVoxelSiteRealizationFacts(plan, 1);

            for (var i = 0; i < plan.Sites.Count; i++)
            {
                PlannedSite site = plan.Sites[i];
                SettlementSiteProjection projection;
                RealizedWorldPoint entrance;
                bool projected = projections.TryProject(site, out projection);
                bool realized = realization.TryGetPublicEntrance(site.RoleId, out entrance);

                Assert.AreEqual(projected, realized, "Role " + site.RoleId + " disagrees about public entrance availability.");
                if (!projected) continue;

                Assert.AreEqual(1, entrance.UnitsPerDecimetre);
                Assert.AreEqual(projection.PublicEntranceDm.X, entrance.Position.X, "Role " + site.RoleId + " entrance X drifted between Architecture and Voxel.");
                Assert.AreEqual(projection.PublicEntranceDm.Y, entrance.Position.Z, "Role " + site.RoleId + " entrance Z drifted between Architecture and Voxel.");
            }
        }

        private static void AssertBespoke(
            int roleId,
            StructureArchetype archetype,
            int envelope,
            int expectedDoorX,
            int expectedDoorZ,
            int expectedDoorHeight,
            int expectedHalfWidth,
            int expectedDepth)
        {
            var intent = new StructureIntent(
                roleId,
                KentridgeDefinition.Id,
                archetype,
                DistrictKind.Civic,
                new Int2(100, 200),
                FrontageDirection.South,
                new Int3(envelope, 200, envelope));
            StructureForm form = BespokeForm(intent);

            StructureSiteGeometry geometry;
            Assert.IsTrue(StructureSiteGeometryResolver.TryResolve(
                intent, KentridgeDefinition.Theme, form, out geometry));
            Assert.AreEqual(100 + expectedDoorX, geometry.PublicEntranceDm.X);
            Assert.AreEqual(200 + expectedDoorZ, geometry.PublicEntranceDm.Y);
            Assert.AreEqual(expectedDoorHeight, geometry.PublicEntranceHeightDm);

            StructureInteriorEnvelope interior;
            Assert.IsTrue(StructureSiteGeometryResolver.TryResolveInterior(
                intent, KentridgeDefinition.Theme, form, out interior));
            Assert.AreEqual(expectedHalfWidth, interior.HalfWidthDm);
            Assert.AreEqual(expectedDepth, interior.DepthDm);
        }

        private static StructureForm BespokeForm(StructureIntent intent) =>
            new StructureForm(
                intent.RoleId,
                intent.Archetype,
                intent.District,
                StructureGenerationMode.Bespoke,
                FootprintForm.Rectangle,
                RoofForm.Gable,
                FrontageRhythm.TwoBay,
                WindowTreatment.Glass,
                0, 0, 0, 0, 0, 0, 0, 0,
                false, false);

        private static bool HasCapability(
            SettlementSiteProjection projection,
            SiteCapabilityKind kind)
        {
            for (var i = 0; i < projection.Capabilities.Count; i++)
                if (projection.Capabilities[i].Kind == kind)
                    return true;
            return false;
        }
    }
}
