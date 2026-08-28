using System;
using System.IO;
using Game.WorldBuilder.Api;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBuilderSceneCompositionTests
    {
        [Test]
        public void RepresentativeEnvironmentRecipesRemainDistinctAndSceneRuntimeOwnsNoGenerationCode()
        {
            const uint seed = 0x5EED1234u;

            WorldEnvironmentSpec small = WorldEnvironmentRecipes.DetailedStructure(seed)
                .With(WorldEnvironmentFeature.Vegetation);
            WorldBuilderEnvironmentComposition.Plan smallPlan =
                WorldBuilderEnvironmentComposition.Resolve(in small);

            Assert.That(smallPlan.Seed, Is.EqualTo(seed));
            Assert.That(smallPlan.ShowcaseContent, Is.EqualTo(ShowcaseFeatureContent.HouseOnly));
            Assert.That(smallPlan.PopulateVegetation, Is.True);
            Assert.That(smallPlan.PopulateAmbientLife, Is.False);
            Assert.That(smallPlan.BuildGalleryDistrict, Is.False);

            WorldEnvironmentSpec landmark = WorldEnvironmentRecipes.FortifiedLandmark(seed + 1u);
            WorldBuilderEnvironmentComposition.Plan landmarkPlan =
                WorldBuilderEnvironmentComposition.Resolve(in landmark);

            Assert.That(landmarkPlan.ShowcaseContent, Is.EqualTo(ShowcaseFeatureContent.CastleOnly));
            Assert.That(landmarkPlan.Seed, Is.EqualTo(seed + 1u));

            WorldEnvironmentSpec gallery = WorldEnvironmentRecipes.GalleryDistrict(seed + 2u);
            WorldBuilderEnvironmentComposition.Plan galleryPlan =
                WorldBuilderEnvironmentComposition.Resolve(in gallery);

            Assert.That(galleryPlan.ShowcaseContent, Is.EqualTo(ShowcaseFeatureContent.Full));
            Assert.That(galleryPlan.PopulateVegetation, Is.True);
            Assert.That(galleryPlan.PopulateAmbientLife, Is.True);
            Assert.That(galleryPlan.BuildGalleryDistrict, Is.True);
            Assert.That(galleryPlan.Seed, Is.EqualTo(seed + 2u));

            Assert.Throws<NotSupportedException>(() =>
            {
                WorldEnvironmentSpec unsupported = new WorldEnvironmentSpec(
                    seed,
                    WorldEnvironmentFeature.Terrain | WorldEnvironmentFeature.Settlement);
                WorldBuilderEnvironmentComposition.Resolve(in unsupported);
            });

            AssertNoRuntimeGenerationSource("Assets/Scenes/Showcase");
            AssertNoRuntimeGenerationSource("Assets/Scenes/Kentridge");
        }

        private static void AssertNoRuntimeGenerationSource(string sceneDirectory)
        {
            if (!Directory.Exists(sceneDirectory)) return;

            string[] files = Directory.GetFiles(sceneDirectory, "*.cs", SearchOption.AllDirectories);
            foreach (string path in files)
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Editor/", StringComparison.Ordinal))
                    continue;

                Assert.Fail(
                    $"Scene runtime source must be orchestration/configuration only and live in " +
                    $"reusable composition ownership; found {normalized}.");
            }
        }
    }
}
