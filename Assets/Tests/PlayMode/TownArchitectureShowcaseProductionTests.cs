using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused production-path guard for the town-architecture showcase. The single-test SceneIssue
    /// request pairs this semantic regression with the standalone WorldbuildingGalleryShowcase capture.
    /// </summary>
    public sealed class TownArchitectureShowcaseProductionTests
    {
        [Test]
        public void SixTownProgramsPreserveRolesFormsDetailsSeedsAndBounds()
        {
            string[] styleIds =
            {
                WorldBuilderTownArchitectureIds.Kentridge,
                WorldBuilderTownArchitectureIds.Hightown,
                WorldBuilderTownArchitectureIds.Moordell,
                WorldBuilderTownArchitectureIds.Rossdam,
                WorldBuilderTownArchitectureIds.FairyVillage,
                WorldBuilderTownArchitectureIds.OrcVillage,
            };
            TownArchitectureStructureRole[] roles =
            {
                TownArchitectureStructureRole.Residential,
                TownArchitectureStructureRole.Commercial,
                TownArchitectureStructureRole.CivicCommunal,
                TownArchitectureStructureRole.LandmarkInfrastructure,
            };

            CollectionAssert.AreEqual(styleIds, WorldBuilderTownArchitecture.AllStyleIds);
            var forms = new HashSet<string>();
            var materials = new HashSet<string>();
            var seeds = new HashSet<uint>();

            foreach (string styleId in styleIds)
            {
                TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(styleId);
                Assert.AreEqual(WorldBuilderTownArchitecture.CanonicalSeed(styleId), program.Seed);
                Assert.AreEqual(1, program.DetailUnitBlocks);
                Assert.GreaterOrEqual(program.ReferenceScreenshots.Count, 5);
                Assert.GreaterOrEqual(program.DetailVocabulary.Count, 10);
                foreach (TownArchitectureStructureRole role in roles)
                    Assert.IsTrue(program.IncludesRole(role), styleId + " missing " + role);

                Assert.IsTrue(forms.Add(program.FormSignature), styleId + " form collapsed onto another town");
                Assert.IsTrue(materials.Add(program.MaterialFamily.Signature), styleId + " material family collapsed onto another town");
                Assert.IsTrue(seeds.Add(program.Seed), styleId + " canonical seed is not unique");
                Assert.AreEqual(program.DeterministicSignature,
                    WorldBuilderTownArchitecture.Resolve(styleId).DeterministicSignature);
            }

            TownArchitectureProgram rossdam = WorldBuilderTownArchitecture.Resolve(WorldBuilderTownArchitectureIds.Rossdam);
            string[] fortificationDetails =
            {
                "arrow-slit-reveal", "layered-coping", "crenellation", "tower-wall-transition",
                "buttress-cap", "access-stair", "gate-frame", "gate-hardware",
            };
            foreach (string detail in fortificationDetails)
                Assert.IsTrue(rossdam.IncludesDetail(detail), "Rossdam missing " + detail);

            Assert.AreEqual(164, TownArchitectureDistrictBounds.WidthVoxels);
            Assert.AreEqual(132, TownArchitectureDistrictBounds.DepthVoxels);
            Assert.AreEqual(78, TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.WidthVoxels, 192);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.DepthVoxels, 160);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels, 96);
        }
    }
}
