using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class GeneratedBuildingVoxelRealizerTests
    {
        [Test]
        public void ArchDoorRealizationDelegatesVoussoirsToCoreEmitter()
        {
            BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(
                Form(17, StructureArchetype.Shop, 112, 2),
                0x51504F50u);
            BuildingOpening door = FindDoor(composition);
            Assert.AreEqual(BuildingDetailSocketKind.ArchBay, door.DetailSocket);

            var archStyle = new ArchFeatureStyle(7, 2, 3, 0);
            var style = new GeneratedBuildingVoxelStyle(
                foundationMaterial: 4,
                wallMaterial: 5,
                windowMaterial: 6,
                wallSurfaceStyle: 1,
                wallCoating: 0,
                foundationHeightVoxels: 3,
                archStyle: archStyle);

            using var output = new NativeList<Primitive>(Allocator.Temp);
            Assert.IsTrue(GeneratedBuildingVoxelRealizer.EmitLocal(
                composition,
                new int3(10, 20, 30),
                decimetresPerVoxel: 1,
                wallDepthVoxels: 6,
                style,
                seed: 0x51504F50u,
                output));

            BuildingDetailRequest request = BuildingDetailLowering.Collect(composition)[0];
            BuildingArchPlacement placement = BuildingArchIntegration.Compile(
                request, 1, 6, archStyle,
                0x51504F50u ^ DetailSeed(door.Storey, door.Bay));

            int wedgeCount = 0;
            int carveCount = 0;
            for (int i = 0; i < output.Length; i++)
            {
                Primitive primitive = output[i];
                if (primitive.Mode == PrimitiveMode.Carve)
                    carveCount++;
                if (primitive.Shape != PrimitiveShape.ArcWedge)
                    continue;

                wedgeCount++;
                Assert.AreEqual(archStyle.StoneMaterial, primitive.Material);
                Assert.AreEqual(archStyle.RingStyle, primitive.SurfaceStyle);
            }

            Assert.AreEqual(placement.Definition.Arch.VoussoirCount, wedgeCount,
                "Generated building realization must use the Core arch emitter, not a box approximation.");
            Assert.GreaterOrEqual(carveCount, composition.Openings.Length,
                "Every semantic facade opening should create a concrete wall carve.");
        }

        [Test]
        public void SameBuildingRealizationProducesIdenticalPrimitiveStream()
        {
            BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(
                Form(29, StructureArchetype.Inn, 132, 3),
                0xA11CEu);
            var style = new GeneratedBuildingVoxelStyle(
                4, 5, 6, 1, 0, 3,
                new ArchFeatureStyle(7, 2, 3, 0));

            using var a = new NativeList<Primitive>(Allocator.Temp);
            using var b = new NativeList<Primitive>(Allocator.Temp);
            GeneratedBuildingVoxelRealizer.EmitLocal(
                composition, new int3(0, 0, 0), 1, 6, style, 0xA11CEu, a);
            GeneratedBuildingVoxelRealizer.EmitLocal(
                composition, new int3(0, 0, 0), 1, 6, style, 0xA11CEu, b);

            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                AssertPrimitiveEqual(a[i], b[i]);
        }

        [Test]
        public void BespokeCompositionIsNotClaimedByGenericRealizer()
        {
            StructureForm massing = new StructureForm(
                99, StructureArchetype.Church, DistrictKind.Civic,
                StructureGenerationMode.Bespoke, FootprintForm.Rectangle, RoofForm.Gable,
                FrontageRhythm.ThreeBay, WindowTreatment.Glass,
                0, 0, 0, 0, 0, 0, 0, 0, false, false);
            BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(massing, 1u);
            var style = new GeneratedBuildingVoxelStyle(
                4, 5, 6, 1, 0, 3,
                new ArchFeatureStyle(7, 2, 3, 0));

            using var output = new NativeList<Primitive>(Allocator.Temp);
            Assert.IsFalse(GeneratedBuildingVoxelRealizer.EmitLocal(
                composition, int3.zero, 1, 6, style, 1u, output));
            Assert.AreEqual(0, output.Length);
        }

        private static StructureForm Form(
            int roleId,
            StructureArchetype archetype,
            int widthDm,
            int storeys)
        {
            return new StructureForm(
                roleId, archetype, DistrictKind.Market,
                StructureGenerationMode.Generated, FootprintForm.Rectangle, RoofForm.Gable,
                FrontageRhythm.ThreeBay, WindowTreatment.Glass,
                widthDm, 72, storeys, 0,
                0, 24, 0, 0, false, false);
        }

        private static BuildingOpening FindDoor(BuildingCompositionForm composition)
        {
            for (int i = 0; i < composition.Openings.Length; i++)
                if (composition.Openings[i].Kind == BuildingOpeningKind.Door)
                    return composition.Openings[i];

            Assert.Fail("Generated composition has no primary door.");
            return default;
        }

        private static uint DetailSeed(int storey, int bay)
        {
            uint h = (uint)(storey + 1) * 0x9E3779B9u
                   ^ (uint)(bay + 1) * 0x85EBCA6Bu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            return h;
        }

        private static void AssertPrimitiveEqual(Primitive expected, Primitive actual)
        {
            Assert.AreEqual(expected.Shape, actual.Shape);
            Assert.AreEqual(expected.Mode, actual.Mode);
            Assert.AreEqual(expected.Material, actual.Material);
            Assert.AreEqual(expected.SurfaceStyle, actual.SurfaceStyle);
            Assert.AreEqual(expected.Coating, actual.Coating);
            Assert.AreEqual(expected.SurfaceFlags, actual.SurfaceFlags);
            Assert.AreEqual(expected.SurfaceDetail, actual.SurfaceDetail);
            Assert.AreEqual(expected.Axis, actual.Axis);
            Assert.AreEqual(expected.Direction, actual.Direction);
            Assert.AreEqual(expected.Profile, actual.Profile);
            Assert.AreEqual(expected.Order, actual.Order);
            Assert.AreEqual(expected.A, actual.A);
            Assert.AreEqual(expected.B, actual.B);
            Assert.AreEqual(expected.Radius, actual.Radius);
            Assert.AreEqual(expected.InnerRadius, actual.InnerRadius);
            Assert.AreEqual(expected.C, actual.C);
            Assert.AreEqual(expected.D, actual.D);
            Assert.AreEqual(expected.StartDirection, actual.StartDirection);
            Assert.AreEqual(expected.EndDirection, actual.EndDirection);
        }
    }
}
