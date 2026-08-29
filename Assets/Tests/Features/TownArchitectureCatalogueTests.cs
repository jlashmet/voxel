using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.Features
{
    public sealed class TownArchitectureCatalogueTests
    {
        private static readonly string[] s_ExpectedStyles =
        {
            WorldBuilderTownArchitectureIds.Kentridge,
            WorldBuilderTownArchitectureIds.Hightown,
            WorldBuilderTownArchitectureIds.Moordell,
            WorldBuilderTownArchitectureIds.Rossdam,
            WorldBuilderTownArchitectureIds.FairyVillage,
            WorldBuilderTownArchitectureIds.OrcVillage,
        };

        private static readonly TownArchitectureStructureRole[] s_ExpectedRoles =
        {
            TownArchitectureStructureRole.Residential,
            TownArchitectureStructureRole.Commercial,
            TownArchitectureStructureRole.CivicCommunal,
            TownArchitectureStructureRole.LandmarkInfrastructure,
        };

        [Test]
        public void CatalogueExposesExactlySixCanonicalReferenceDrivenStyles()
        {
            CollectionAssert.AreEqual(s_ExpectedStyles, WorldBuilderTownArchitecture.AllStyleIds);

            var forms = new HashSet<string>();
            var materials = new HashSet<string>();
            var seeds = new HashSet<uint>();

            foreach (string styleId in s_ExpectedStyles)
            {
                TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(styleId);

                Assert.AreEqual(styleId, program.StyleId);
                Assert.AreEqual(WorldBuilderTownArchitecture.CanonicalSeed(styleId), program.Seed);
                Assert.AreEqual(1, program.DetailUnitBlocks, styleId + " must preserve one-voxel (~10 cm) detail units");
                Assert.GreaterOrEqual(program.ReferenceScreenshots.Count, 5, styleId + " must remain reference-driven");
                Assert.GreaterOrEqual(program.DetailVocabulary.Count, 10, styleId + " must expose reusable player-scale detail vocabulary");

                foreach (TownArchitectureStructureRole role in s_ExpectedRoles)
                    Assert.IsTrue(program.IncludesRole(role), styleId + " is missing required role " + role);

                Assert.IsTrue(forms.Add(program.FormSignature), styleId + " collapsed onto another town form signature");
                Assert.IsTrue(materials.Add(program.MaterialFamily.Signature), styleId + " collapsed onto another town material family");
                Assert.IsTrue(seeds.Add(program.Seed), styleId + " must have a distinct canonical evidence seed");

                TownArchitectureProgram repeated = WorldBuilderTownArchitecture.Resolve(styleId);
                Assert.AreEqual(program.DeterministicSignature, repeated.DeterministicSignature,
                    styleId + " program must resolve deterministically");
            }

            Assert.AreEqual(6, forms.Count);
            Assert.AreEqual(6, materials.Count);
            Assert.AreEqual(6, seeds.Count);
        }

        [Test]
        public void RossdamCarriesReusableFortificationConstructionVocabulary()
        {
            TownArchitectureProgram rossdam = WorldBuilderTownArchitecture.Resolve(WorldBuilderTownArchitectureIds.Rossdam);

            Assert.AreEqual(TownArchitectureSilhouette.RoyalFortified, rossdam.Silhouette);
            Assert.AreEqual(TownArchitectureRoofForm.FortifiedParapet, rossdam.RoofForm);
            Assert.AreEqual(TownArchitectureOpeningStyle.FortifiedReveal, rossdam.OpeningStyle);

            string[] requiredDetails =
            {
                "arrow-slit-reveal",
                "layered-coping",
                "crenellation",
                "tower-wall-transition",
                "buttress-cap",
                "access-stair",
                "gate-frame",
                "gate-hardware",
            };

            foreach (string detail in requiredDetails)
                Assert.IsTrue(rossdam.IncludesDetail(detail), "Rossdam is missing reusable fortification detail " + detail);
        }

        [Test]
        public void DistrictFootprintIsPublicAndBoundedForSafePlacement()
        {
            Assert.AreEqual(82, TownArchitectureDistrictBounds.HalfWidthVoxels);
            Assert.AreEqual(66, TownArchitectureDistrictBounds.HalfDepthVoxels);
            Assert.AreEqual(164, TownArchitectureDistrictBounds.WidthVoxels);
            Assert.AreEqual(132, TownArchitectureDistrictBounds.DepthVoxels);
            Assert.AreEqual(78, TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels);

            Assert.LessOrEqual(TownArchitectureDistrictBounds.WidthVoxels, 192);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.DepthVoxels, 160);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels, 96);
        }
    }
}
