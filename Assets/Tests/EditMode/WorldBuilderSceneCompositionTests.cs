using System;
using System.IO;
using Game.Composition.WorldBuilderWorldGen;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class WorldBuilderSceneCompositionTests
    {
        private static readonly string[] ForbiddenSceneGenerationMarkers =
        {
            "VoxelEngineBootstrap.CreateStorage",
            "new ShowcaseWorld(",
            "ShowcaseVoxelAuthoringSession(",
            "VegetationPlacement.Generate(",
            "VegetationComposition.ReplaceTreeWorld(",
            "KentridgeCombinedVoxelCatalogue.Build(",
            "HightownVoxelCatalogue.Build(",
            "RegionCorridorCatalogue.Build(",
            "SettlementCatalogueCombiner.Combine(",
            "ConfigureGeneratedContentForGameplay(",
            "FeatureCatalogue",
            "AuthorTerrain(",
            "SetVoxel(",
            "new Mesh(",
        };

        [Test]
        public void RepresentativeEnvironmentRecipesRemainDistinctAndSceneSourceOwnsNoGenerationBackends()
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

            WorldEnvironmentSpec serializedFull = WorldBuilderEnvironmentComposition.SemanticSpec(
                seed + 3u,
                ShowcaseFeatureContent.Full);
            WorldBuilderEnvironmentComposition.Plan serializedFullPlan =
                WorldBuilderEnvironmentComposition.Resolve(in serializedFull);
            Assert.That(serializedFullPlan.ShowcaseContent, Is.EqualTo(ShowcaseFeatureContent.Full));
            Assert.That(serializedFullPlan.Seed, Is.EqualTo(seed + 3u));

            Assert.Throws<NotSupportedException>(() =>
            {
                WorldEnvironmentSpec unsupported = new WorldEnvironmentSpec(
                    seed,
                    WorldEnvironmentFeature.Terrain | WorldEnvironmentFeature.Settlement);
                WorldBuilderEnvironmentComposition.Resolve(in unsupported);
            });

            Assert.That(
                File.Exists("Assets/Game/Composition/Showcase/SceneRuntime/VoxelShowcase.cs"),
                Is.True,
                "Showcase runtime composition must live under reusable Game composition ownership.");
            Assert.That(
                File.Exists("Assets/Game/Composition/Kentridge/Playable/SceneRuntime/KentridgePlayableSlice.cs"),
                Is.True,
                "Kentridge runtime composition must live under reusable Game composition ownership.");

            AssertSceneSourceContainsNoGenerationBackends("Assets/Scenes");
        }

        [Test]
        public void KentridgeAndHightownAreBothAuthoredThroughWorldBuilderAndStayDistinct()
        {
            const uint seed = 0x4B454E54u;

            AuthoredTownPlan kentridge = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Kentridge,
                seed);
            AuthoredTownPlan hightown = WorldBuilderTownAuthoring.Author(
                WorldBuilderTownIds.Hightown,
                seed);

            Assert.That(kentridge.SettlementId, Is.EqualTo(WorldBuilderTownIds.Kentridge));
            Assert.That(hightown.SettlementId, Is.EqualTo(WorldBuilderTownIds.Hightown));
            Assert.That(kentridge.Seed, Is.EqualTo(seed));
            Assert.That(hightown.Seed, Is.EqualTo(seed));

            Assert.That(kentridge.BackendPlan, Is.TypeOf<SettlementPlan>());
            Assert.That(hightown.BackendPlan, Is.TypeOf<SettlementPlan>());
            var kentridgePlan = (SettlementPlan)kentridge.BackendPlan;
            var hightownPlan = (SettlementPlan)hightown.BackendPlan;

            Assert.That(kentridgePlan.CentreDm.Equals(hightownPlan.CentreDm), Is.False,
                "Distinct WorldBuilder recipes must not collapse both towns onto one canonical composition.");
            Assert.That(kentridgePlan.Plots.Count, Is.GreaterThan(0));
            Assert.That(hightownPlan.Plots.Count, Is.GreaterThan(0));

            Assert.That(kentridgePlan.Streets.Count, Is.Zero,
                "The modern Kentridge recipe must remain organic rather than restoring legacy streets to satisfy traversal.");
            Assert.That(kentridgePlan.Routes.Count, Is.GreaterThan(0));

            var projections = new KentridgeArchitectureSiteProjectionProvider(kentridgePlan);
            var traversal = new SettlementStreetTraversalFacts(kentridgePlan, projections);
            int pubRole = (int)KentridgeRole.Pub;
            for (var i = 0; i < kentridgePlan.Sites.Count; i++)
            {
                PlannedSite site = kentridgePlan.Sites[i];
                Assert.That(
                    traversal.IsReachable(pubRole, site.RoleId, TraversalProfile.NormalParty),
                    Is.True,
                    $"WorldBuilder traversal must connect Kentridge pub to role {site.RoleId} through inferred routes/plaza.");
                Assert.That(
                    traversal.TraversalDistanceMetres(pubRole, site.RoleId, TraversalProfile.NormalParty),
                    Is.LessThan(int.MaxValue));
            }
        }

        private static void AssertSceneSourceContainsNoGenerationBackends(string sceneDirectory)
        {
            if (!Directory.Exists(sceneDirectory)) return;

            string[] files = Directory.GetFiles(sceneDirectory, "*.cs", SearchOption.AllDirectories);
            foreach (string path in files)
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Editor/", StringComparison.Ordinal))
                    continue;

                string source = File.ReadAllText(path);
                foreach (string marker in ForbiddenSceneGenerationMarkers)
                {
                    Assert.That(
                        source.Contains(marker, StringComparison.Ordinal),
                        Is.False,
                        $"Scene source may orchestrate/present but must not own generated world backend '{marker}': {normalized}");
                }
            }
        }
    }
}
