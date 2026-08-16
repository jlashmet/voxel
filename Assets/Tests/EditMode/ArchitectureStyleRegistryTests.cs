using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ArchitectureStyleRegistryTests
    {
        private const uint Seed = 0x5354594Cu;

        [Test]
        public void CustomStyleResolvesNamedStructureAndGeometryWithoutKentridgeDispatch()
        {
            var compiler = new TestStyleCompiler();
            var styles = new ArchitectureStyleRegistry(compiler);
            var intent = new StructureIntent(
                roleId: 42,
                styleId: compiler.StyleId,
                archetype: StructureArchetype.Townhouse,
                district: DistrictKind.Residential,
                positionDm: new Int2(100, 200),
                frontage: FrontageDirection.East,
                envelopeDm: new Int3(140, 100, 130));

            StructureForm form = ArchitectureCompiler.Resolve(
                intent,
                KentridgeDefinition.Theme,
                Seed,
                styles);
            StructureGeometryProfile geometry =
                styles.Require(intent.StyleId).ResolveGeometry(intent, form);

            Assert.AreEqual(42, form.RoleId);
            Assert.AreEqual(StructureArchetype.Townhouse, form.Archetype);
            Assert.AreEqual(DistrictKind.Residential, form.District);
            Assert.AreEqual(StructureGenerationMode.Generated, form.Mode);
            Assert.AreEqual(78, form.WidthDm);
            Assert.AreEqual(5, geometry.ShellCornerRadiusDm,
                "A non-Kentridge style should own its own low-level shell geometry.");
            Assert.AreEqual(StructureSurfaceTreatment.MasonryJoint, geometry.ShellSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Planar, geometry.RoofSurface);
            Assert.IsTrue(compiler.NamedValidated,
                "The shared compiler must still enforce style-specific validation hooks.");
        }

        [Test]
        public void CustomStyleResolvesAnonymousFabricWithoutKentridgeDispatch()
        {
            var compiler = new TestStyleCompiler();
            var styles = new ArchitectureStyleRegistry(compiler);
            var intent = new UrbanFabricIntent(
                compiler.StyleId,
                DistrictKind.Market,
                minStoreys: 2,
                maxStoreys: 4,
                envelopeDm: 100,
                variationContext: 7);

            UrbanFabricForm form = UrbanFabricCompiler.Resolve(
                intent,
                Seed,
                runIndex: 3,
                siteIndex: 9,
                styles: styles);

            Assert.AreEqual(3, form.Storeys);
            Assert.AreEqual(62, form.WidthDm);
            Assert.AreEqual(54, form.DepthDm);
            Assert.IsTrue(form.HasAwning);
            Assert.IsTrue(compiler.FabricValidated,
                "Anonymous frontage must use the registered style validation hook.");
        }

        [Test]
        public void CustomStyleOwnsAnonymousFabricGeometryWithoutVoxelDependencies()
        {
            var compiler = new TestStyleCompiler();
            var styles = new ArchitectureStyleRegistry(compiler);
            var intent = new UrbanFabricIntent(
                compiler.StyleId,
                DistrictKind.Market,
                minStoreys: 2,
                maxStoreys: 4,
                envelopeDm: 100,
                variationContext: 7);
            UrbanFabricForm form = UrbanFabricCompiler.Resolve(
                intent,
                Seed,
                runIndex: 3,
                siteIndex: 9,
                styles: styles);

            StructureGeometryProfile geometry = UrbanFabricGeometryProfiles.Resolve(
                intent,
                form,
                styles);

            Assert.AreEqual(6, geometry.ShellCornerRadiusDm);
            Assert.AreEqual(4, geometry.OpeningCornerRadiusDm);
            Assert.AreEqual(StructureSurfaceTreatment.ArchitecturalRounded,
                geometry.ShellSurface);
            Assert.AreEqual(StructureSurfaceTreatment.ArchitecturalRounded,
                geometry.OpeningSurface);
            Assert.AreEqual(StructureSurfaceTreatment.Smooth, geometry.RoofSurface);
        }

        [Test]
        public void RegistryExtensionIsImmutableAndRejectsDuplicateStyleIds()
        {
            var first = new TestStyleCompiler("test.city.one");
            var second = new TestStyleCompiler("test.city.two");
            var original = new ArchitectureStyleRegistry(first);
            ArchitectureStyleRegistry extended = original.With(second);

            Assert.AreEqual(1, original.Count);
            Assert.AreEqual(2, extended.Count);
            Assert.IsFalse(original.TryResolve(second.StyleId, out _));
            Assert.IsTrue(extended.TryResolve(second.StyleId, out _));

            Assert.Throws<System.ArgumentException>(() => original.With(
                new TestStyleCompiler(first.StyleId)));
        }

        private sealed class TestStyleCompiler :
            IArchitectureStyleCompiler,
            IUrbanFabricGeometryProfileResolver
        {
            public TestStyleCompiler(string styleId = "test.city")
            {
                StyleId = styleId;
            }

            public string StyleId { get; }
            public bool NamedValidated { get; private set; }
            public bool FabricValidated { get; private set; }

            public StructureForm ResolveStructure(
                StructureIntent intent,
                ArchitectureTheme theme,
                uint seed)
            {
                return new StructureForm(
                    intent.RoleId,
                    intent.Archetype,
                    intent.District,
                    StructureGenerationMode.Generated,
                    FootprintForm.Rectangle,
                    RoofForm.Gable,
                    FrontageRhythm.ThreeBay,
                    WindowTreatment.Glass,
                    widthDm: 78,
                    depthDm: 68,
                    storeys: 2,
                    doorOffsetDm: 0,
                    upperOverhangDm: 2,
                    roofHeightDm: 24,
                    wingWidthDm: 0,
                    wingDepthDm: 0,
                    wingOnRight: false,
                    chimneyOnRight: true);
            }

            public void ValidateStructure(
                StructureIntent intent,
                ArchitectureTheme theme,
                StructureForm form)
            {
                NamedValidated = true;
            }

            public StructureGeometryProfile ResolveGeometry(
                StructureIntent intent,
                StructureForm form)
            {
                return new StructureGeometryProfile(
                    foundationCornerRadiusDm: 2,
                    shellCornerRadiusDm: 5,
                    openingCornerRadiusDm: 3,
                    detailCornerRadiusDm: 1,
                    foundationSurface: StructureSurfaceTreatment.Beveled,
                    shellSurface: StructureSurfaceTreatment.MasonryJoint,
                    openingSurface: StructureSurfaceTreatment.Rounded,
                    detailSurface: StructureSurfaceTreatment.Smooth,
                    roofSurface: StructureSurfaceTreatment.Planar);
            }

            public UrbanFabricForm ResolveUrbanFabric(
                UrbanFabricIntent intent,
                uint seed,
                int runIndex,
                int siteIndex)
            {
                return new UrbanFabricForm(
                    widthDm: 62,
                    depthDm: 54,
                    storeys: 3,
                    upperOverhangDm: 1,
                    roofHeightDm: 22,
                    roof: RoofForm.SteepGable,
                    frontageRhythm: FrontageRhythm.Asymmetric,
                    windowTreatment: WindowTreatment.Warm,
                    hasAwning: true,
                    chimneyOnRight: false,
                    annexOnRight: true);
            }

            public StructureGeometryProfile ResolveUrbanFabricGeometry(
                UrbanFabricIntent intent,
                UrbanFabricForm form)
            {
                return new StructureGeometryProfile(
                    foundationCornerRadiusDm: 2,
                    shellCornerRadiusDm: 6,
                    openingCornerRadiusDm: 4,
                    detailCornerRadiusDm: 2,
                    foundationSurface: StructureSurfaceTreatment.Beveled,
                    shellSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                    openingSurface: StructureSurfaceTreatment.ArchitecturalRounded,
                    detailSurface: StructureSurfaceTreatment.Beveled,
                    roofSurface: StructureSurfaceTreatment.Smooth);
            }

            public void ValidateUrbanFabric(UrbanFabricIntent intent, UrbanFabricForm form)
            {
                FabricValidated = true;
                if (form.WidthDm > intent.EnvelopeDm || form.DepthDm > intent.EnvelopeDm)
                    throw new System.InvalidOperationException("Test style escaped its envelope.");
            }
        }
    }
}
