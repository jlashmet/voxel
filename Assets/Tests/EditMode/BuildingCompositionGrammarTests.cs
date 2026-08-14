using System;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class BuildingCompositionGrammarTests
    {
        [Test]
        public void SameSeedProducesIdenticalComposition()
        {
            StructureForm massing = Form(
                roleId: 41,
                archetype: StructureArchetype.Townhouse,
                rhythm: FrontageRhythm.ThreeBay,
                widthDm: 88,
                storeys: 3,
                doorOffsetDm: 8);

            BuildingCompositionForm a = BuildingCompositionCompiler.Resolve(massing, 0xB17D1A6u);
            BuildingCompositionForm b = BuildingCompositionCompiler.Resolve(massing, 0xB17D1A6u);

            Assert.AreEqual(a.StoreyHeightDm, b.StoreyHeightDm);
            Assert.AreEqual(a.BayCount, b.BayCount);
            Assert.AreEqual(a.BayWidthDm, b.BayWidthDm);
            Assert.AreEqual(a.Openings.Length, b.Openings.Length);
            for (int i = 0; i < a.Openings.Length; i++)
                AssertOpeningEqual(a.Openings[i], b.Openings[i]);
        }

        [Test]
        public void GeneratedFacadeOpeningsStayWithinMassing()
        {
            StructureForm[] forms =
            {
                Form(1, StructureArchetype.Townhouse, FrontageRhythm.TwoBay, 66, 2, -8),
                Form(2, StructureArchetype.WideHouse, FrontageRhythm.ThreeBay, 84, 2, 0),
                Form(3, StructureArchetype.Shop, FrontageRhythm.Asymmetric, 112, 3, 10),
                Form(4, StructureArchetype.Inn, FrontageRhythm.ThreeBay, 132, 3, 0),
            };

            for (int f = 0; f < forms.Length; f++)
            {
                BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(
                    forms[f], (uint)(0xCAFE0000u + f));
                BuildingCompositionCompiler.Validate(composition);

                Assert.AreEqual(composition.BayCount * forms[f].Storeys, composition.Openings.Length);
                int doors = 0;
                int halfWidth = forms[f].WidthDm / 2;
                for (int i = 0; i < composition.Openings.Length; i++)
                {
                    BuildingOpening opening = composition.Openings[i];
                    Assert.LessOrEqual(
                        Math.Abs(opening.CenterOffsetDm) + opening.WidthDm / 2,
                        halfWidth,
                        "Opening escaped the generated frontage.");
                    Assert.LessOrEqual(
                        opening.SillHeightDm + opening.HeightDm,
                        composition.StoreyHeightDm,
                        "Opening escaped its local storey.");
                    if (opening.Kind == BuildingOpeningKind.Door)
                    {
                        doors++;
                        Assert.AreEqual(0, opening.Storey);
                    }
                }

                Assert.AreEqual(1, doors, "A generated frontage should contain exactly one primary door.");
            }
        }

        [Test]
        public void ShopDoorRequestsArchBayAndWindowsDoNot()
        {
            BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(
                Form(17, StructureArchetype.Shop, FrontageRhythm.ThreeBay, 112, 2, 0),
                0x51504F50u);

            int archDoors = 0;
            for (int i = 0; i < composition.Openings.Length; i++)
            {
                BuildingOpening opening = composition.Openings[i];
                if (opening.Kind == BuildingOpeningKind.Door)
                {
                    Assert.AreEqual(BuildingDetailSocketKind.ArchBay, opening.DetailSocket);
                    archDoors++;
                }
                else
                {
                    Assert.AreEqual(BuildingDetailSocketKind.None, opening.DetailSocket);
                }
            }

            Assert.AreEqual(1, archDoors);
        }

        [Test]
        public void HouseArchVariationIsSeededAndBounded()
        {
            StructureForm massing = Form(
                73, StructureArchetype.Townhouse, FrontageRhythm.ThreeBay, 96, 2, 0);
            bool sawArch = false;
            bool sawPlain = false;

            for (uint seed = 1; seed <= 64; seed++)
            {
                BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(massing, seed);
                BuildingCompositionCompiler.Validate(composition);
                BuildingCompositionForm repeat = BuildingCompositionCompiler.Resolve(massing, seed);

                bool arch = false;
                for (int i = 0; i < composition.Openings.Length; i++)
                {
                    AssertOpeningEqual(composition.Openings[i], repeat.Openings[i]);
                    if (composition.Openings[i].DetailSocket == BuildingDetailSocketKind.ArchBay)
                        arch = true;
                }

                sawArch |= arch;
                sawPlain |= !arch;
            }

            Assert.IsTrue(sawArch, "Seeded house variation should be able to request an arch portal.");
            Assert.IsTrue(sawPlain, "Seeded house variation should not stamp arches onto every house.");
        }

        [Test]
        public void DetailLoweringPreservesSemanticSocketGeometry()
        {
            BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(
                Form(29, StructureArchetype.Inn, FrontageRhythm.ThreeBay, 132, 3, 0),
                0xA11CEu);

            MountingForce.WorldGen.Voxel.BuildingDetailRequest[] requests =
                MountingForce.WorldGen.Voxel.BuildingDetailLowering.Collect(composition);

            Assert.AreEqual(1, requests.Length);
            MountingForce.WorldGen.Voxel.BuildingDetailRequest request = requests[0];
            Assert.AreEqual(BuildingDetailSocketKind.ArchBay, request.Kind);

            BuildingOpening door = FindDoor(composition);
            Assert.AreEqual(door.Storey, request.Storey);
            Assert.AreEqual(door.Bay, request.Bay);
            Assert.AreEqual(door.CenterOffsetDm, request.CenterOffsetDm);
            Assert.AreEqual(
                door.Storey * composition.StoreyHeightDm + door.SillHeightDm,
                request.BaseHeightDm);
            Assert.AreEqual(door.WidthDm, request.WidthDm);
            Assert.AreEqual(door.HeightDm, request.HeightDm);
        }

        [Test]
        public void BespokeMassingProducesNoGeneratedFacadeOrDetailRequests()
        {
            StructureForm massing = new StructureForm(
                99, StructureArchetype.Townhouse, DistrictKind.Civic,
                StructureGenerationMode.Bespoke, FootprintForm.Rectangle, RoofForm.Gable,
                FrontageRhythm.TwoBay, WindowTreatment.Glass,
                0, 0, 0, 0, 0, 0, 0, 0, false, false);

            BuildingCompositionForm composition = BuildingCompositionCompiler.Resolve(massing, 1u);
            Assert.AreEqual(0, composition.BayCount);
            Assert.AreEqual(0, composition.Openings.Length);
            Assert.AreEqual(0, MountingForce.WorldGen.Voxel.BuildingDetailLowering.Collect(composition).Length);
        }

        private static StructureForm Form(
            int roleId,
            StructureArchetype archetype,
            FrontageRhythm rhythm,
            int widthDm,
            int storeys,
            int doorOffsetDm)
        {
            return new StructureForm(
                roleId, archetype, DistrictKind.Market,
                StructureGenerationMode.Generated, FootprintForm.Rectangle, RoofForm.Gable,
                rhythm, WindowTreatment.Glass,
                widthDm, 72, storeys, doorOffsetDm,
                0, 24, 0, 0, false, false);
        }

        private static BuildingOpening FindDoor(BuildingCompositionForm composition)
        {
            for (int i = 0; i < composition.Openings.Length; i++)
                if (composition.Openings[i].Kind == BuildingOpeningKind.Door)
                    return composition.Openings[i];

            Assert.Fail("Generated composition has no door.");
            return default;
        }

        private static void AssertOpeningEqual(BuildingOpening expected, BuildingOpening actual)
        {
            Assert.AreEqual(expected.Kind, actual.Kind);
            Assert.AreEqual(expected.DetailSocket, actual.DetailSocket);
            Assert.AreEqual(expected.Storey, actual.Storey);
            Assert.AreEqual(expected.Bay, actual.Bay);
            Assert.AreEqual(expected.CenterOffsetDm, actual.CenterOffsetDm);
            Assert.AreEqual(expected.SillHeightDm, actual.SillHeightDm);
            Assert.AreEqual(expected.WidthDm, actual.WidthDm);
            Assert.AreEqual(expected.HeightDm, actual.HeightDm);
        }
    }
}
