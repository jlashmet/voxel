using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;

namespace Game.Materials.Tests
{
    public sealed class GameMaterialRenderingTests
    {
        [Test]
        public void RenderingDefinitions_CoverEveryCanonicalIdExactlyOnce()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            Assert.That(definitions.Length, Is.EqualTo(GameMaterialCatalogue.Count));

            var seen = new bool[GameMaterialCatalogue.Count];
            for (int i = 0; i < definitions.Length; i++)
            {
                byte materialId = definitions[i].MaterialIndex;
                Assert.That(materialId, Is.LessThan(GameMaterialCatalogue.Count));
                Assert.That(seen[materialId], Is.False,
                    $"Duplicate rendering definition for {GameMaterialCatalogue.NameOf(materialId)} ({materialId}).");
                seen[materialId] = true;
            }

            for (byte materialId = 0; materialId < GameMaterialCatalogue.Count; materialId++)
                Assert.That(seen[materialId], Is.True,
                    $"Missing rendering definition for {GameMaterialCatalogue.NameOf(materialId)} ({materialId}).");
        }

        [Test]
        public void RenderingDefinitions_PreserveCurrentShowcaseRows()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();

            MaterialPresentationDefinition stone = definitions[GameMaterialIds.Stone];
            Assert.That(stone.MaterialIndex, Is.EqualTo(GameMaterialIds.Stone));
            Assert.That(stone.Sampling.x, Is.EqualTo(0f));
            Assert.That(stone.Sampling.z, Is.EqualTo((float)MaterialTextureProjection.Triplanar));
            Assert.That(stone.Albedo.x, Is.EqualTo(0.43f).Within(0.0001f));

            MaterialPresentationDefinition wood = definitions[GameMaterialIds.Wood];
            Assert.That(wood.Sampling.x, Is.EqualTo(1f));
            Assert.That(wood.Sampling.z, Is.EqualTo((float)MaterialTextureProjection.Face));

            MaterialPresentationDefinition flower = definitions[GameMaterialIds.FlowerWhite];
            Assert.That(flower.Albedo.x, Is.EqualTo(1f));
            Assert.That(flower.Albedo.y, Is.EqualTo(1f));
            Assert.That(flower.Albedo.z, Is.EqualTo(1f));
        }

        [Test]
        public void WaterProfiles_AreInstalledThroughOneSharedDataDrivenRendererContract()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            MaterialPresentationDefinition still = definitions[GameMaterialIds.Water];
            MaterialPresentationDefinition river = definitions[GameMaterialIds.RiverWater];
            MaterialPresentationDefinition waterfall = definitions[GameMaterialIds.Cascade];

            Assert.That(still.Water.Profile, Is.EqualTo(WaterPresentationProfile.Still));
            Assert.That(river.Water.Profile, Is.EqualTo(WaterPresentationProfile.Flowing));
            Assert.That(waterfall.Water.Profile, Is.EqualTo(WaterPresentationProfile.Waterfall));
            Assert.That(river.Water.Motion.w, Is.GreaterThan(still.Water.Motion.w * 4f),
                "A river must be a reusable directional-flow profile, not a faster lake branch in shader code.");
            Assert.That(waterfall.Water.Cascade.x, Is.GreaterThan(0.8f));
            Assert.That(waterfall.Water.Cascade.y, Is.GreaterThan(0.8f));
            Assert.That(waterfall.Water.Cascade.z, Is.GreaterThan(0.9f));
            Assert.That(waterfall.Water.Cascade.w, Is.GreaterThan(0.5f));

            VoxelMaterialPresentationInstaller.Apply(definitions);
            Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(GameMaterialIds.Water), Is.True);
            Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(GameMaterialIds.RiverWater), Is.True);
            Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(GameMaterialIds.Cascade), Is.True);
            Assert.That(VoxelPresentationCatalogue.IsWaterMaterial(GameMaterialIds.Stone), Is.False);
            uint expectedMask = (1u << GameMaterialIds.Water)
                              | (1u << GameMaterialIds.RiverWater)
                              | (1u << GameMaterialIds.Cascade);
            Assert.That(VoxelPresentationCatalogue.WaterMaterialMask, Is.EqualTo(expectedMask));
        }

        [Test]
        public void WaterProfiles_UseOpaqueMaterialIdsAndCanBeRemappedByPresentationData()
        {
            const byte firstOpaqueId = 3;
            const byte secondOpaqueId = 27;
            float4 albedo = new(0.2f, 0.5f, 0.7f, 1f);
            var still = new WaterPresentationDefinition(
                WaterPresentationProfile.Still,
                new float4(0.2f, 0.7f, 0.8f, 0.45f),
                new float4(0.03f, 0.18f, 0.24f, 2.5f),
                new float2(1f, 0f),
                0.15f,
                1.2f,
                0.5f,
                0.08f,
                0.82f,
                0.2f,
                0.3f,
                2f,
                0.4f);
            var waterfall = new WaterPresentationDefinition(
                WaterPresentationProfile.Waterfall,
                new float4(0.25f, 0.72f, 0.84f, 0.72f),
                new float4(0.05f, 0.22f, 0.28f, 1.6f),
                new float2(0f, 1f),
                1.9f,
                1.6f,
                0.9f,
                0.04f,
                0.74f,
                0.6f,
                0.75f,
                3f,
                1.1f,
                turbulence: 0.95f,
                edgeFoam: 0.9f,
                impactFoam: 1f,
                mist: 0.7f);

            try
            {
                VoxelMaterialPresentationInstaller.Apply(new[]
                {
                    new MaterialPresentationDefinition(firstOpaqueId, albedo, water: still),
                    new MaterialPresentationDefinition(secondOpaqueId, albedo, water: still),
                });

                uint expectedMask = (1u << firstOpaqueId) | (1u << secondOpaqueId);
                Assert.That(VoxelPresentationCatalogue.WaterMaterialMask, Is.EqualTo(expectedMask));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[firstOpaqueId].x,
                    Is.EqualTo((float)WaterPresentationProfile.Still));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[secondOpaqueId].x,
                    Is.EqualTo((float)WaterPresentationProfile.Still));

                VoxelMaterialPresentationInstaller.Apply(new[]
                {
                    new MaterialPresentationDefinition(firstOpaqueId, albedo, water: waterfall),
                    new MaterialPresentationDefinition(secondOpaqueId, albedo, water: still),
                });

                Assert.That(VoxelPresentationCatalogue.WaterMaterialMask, Is.EqualTo(expectedMask));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[firstOpaqueId].x,
                    Is.EqualTo((float)WaterPresentationProfile.Waterfall));
                Assert.That(VoxelPresentationCatalogue.WaterMotion[secondOpaqueId].x,
                    Is.EqualTo((float)WaterPresentationProfile.Still));
                Assert.That(VoxelPresentationCatalogue.WaterCascade[firstOpaqueId].x, Is.EqualTo(0.95f));
                Assert.That(VoxelPresentationCatalogue.WaterCascade[secondOpaqueId], Is.EqualTo(UnityEngine.Vector4.zero));
            }
            finally
            {
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        [Test]
        public void RiverProfile_ReusesWaterSimulation_WhileCascadePreservesInertGameplay()
        {
            ref readonly GameMaterialRuntimeDefinition still =
                ref GameMaterialRuntimeCatalogue.Get(GameMaterialIds.Water);
            ref readonly GameMaterialRuntimeDefinition river =
                ref GameMaterialRuntimeCatalogue.Get(GameMaterialIds.RiverWater);
            ref readonly GameMaterialRuntimeDefinition waterfall =
                ref GameMaterialRuntimeCatalogue.Get(GameMaterialIds.Cascade);

            Assert.That(still.Simulation.DestructionClass, Is.EqualTo(DestructionClass.Spreading));
            Assert.That(river.Simulation.DestructionClass, Is.EqualTo(still.Simulation.DestructionClass));
            Assert.That(river.Simulation.Hardness, Is.EqualTo(still.Simulation.Hardness));
            Assert.That(waterfall.Simulation.DestructionClass, Is.EqualTo(DestructionClass.None),
                "Presentation integration must not silently turn authored cascades into simulated spreading water.");
        }

        [Test]
        public void GrassAndMossCoatingShareAuthoredTextureDensity()
        {
            MaterialPresentationDefinition grass =
                GameMaterialRenderingDefinitions.Create()[GameMaterialIds.Grass];
            UnityEngine.Vector4 mossSampling = VoxelPresentationCatalogue.CoatingSampling[1];

            Assert.That(mossSampling.x, Is.EqualTo(grass.Sampling.x),
                "The renderer-owned moss coating must reuse the authored grass texture layer.");
            Assert.That(mossSampling.y, Is.EqualTo(grass.Surface.x).Within(0.0001f),
                "Crossing a moss coating boundary must not change the apparent grass motif size.");
        }

        [Test]
        public void TerrainRowsKeepResolvableDetailWithoutDominantNormalRelief()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();

            foreach (byte terrain in new[]
                     {
                         GameMaterialIds.Sand,
                         GameMaterialIds.Grass,
                         GameMaterialIds.Dirt,
                     })
            {
                MaterialPresentationDefinition row = definitions[terrain];
                Assert.That(row.Sampling.z,
                    Is.EqualTo((float)MaterialTextureProjection.Triplanar));
                Assert.That(row.Sampling.w, Is.LessThanOrEqualTo(0.16f));
                Assert.That(row.Surface.y, Is.InRange(0.05f, 0.08f));
                Assert.That(row.Surface.w, Is.EqualTo(1f),
                    "Terrain detail should modulate luminance without importing source hue.");
                Assert.That(row.Surface.x, Is.GreaterThan(1f / 16f),
                    "Ground texture is tiled too large to resolve at eye level.");
            }

            MaterialPresentationDefinition window = definitions[GameMaterialIds.LitWindow];
            Assert.That(window.Albedo.x, Is.GreaterThan(0.9f));
            Assert.That(window.Albedo.y, Is.GreaterThan(0.45f));
            Assert.That(window.Albedo.z, Is.LessThan(0.25f));
        }
    }
}
