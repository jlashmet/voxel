using System;
using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;

namespace Game.Materials.Tests
{
    public sealed class MaterialPresentationInstallerTests
    {
        [Test]
        public void Apply_UsesGameRowsAndNeutralizesUnassignedEngineRows()
        {
            MaterialPresentationDefinition[] definitions = GameMaterialRenderingDefinitions.Create();
            VoxelMaterialPresentationInstaller.Apply(definitions);

            MaterialPresentationDefinition stone = definitions[GameMaterialIds.Stone];
            Vector4 actualStone = VoxelPresentationCatalogue.MaterialAlbedo[GameMaterialIds.Stone];
            Assert.That(actualStone.x, Is.EqualTo(stone.Albedo.x).Within(0.0001f));
            Assert.That(actualStone.y, Is.EqualTo(stone.Albedo.y).Within(0.0001f));
            Assert.That(actualStone.z, Is.EqualTo(stone.Albedo.z).Within(0.0001f));

            int unassignedRow = VoxelPresentationCatalogue.MaxMaterials - 1;
            Assert.That(unassignedRow, Is.GreaterThanOrEqualTo(GameMaterialCatalogue.Count));
            Assert.That(VoxelPresentationCatalogue.MaterialAlbedo[unassignedRow],
                Is.EqualTo(new Vector4(1f, 1f, 1f, 1f)));
            Assert.That(VoxelPresentationCatalogue.MaterialSampling[unassignedRow], Is.EqualTo(Vector4.zero));
        }

        [Test]
        public void Apply_RejectsDuplicateOpaqueMaterialIndices()
        {
            var duplicate = new[]
            {
                new MaterialPresentationDefinition(1, new Unity.Mathematics.float4(1f, 0f, 0f, 1f)),
                new MaterialPresentationDefinition(1, new Unity.Mathematics.float4(0f, 1f, 0f, 1f)),
            };

            Assert.Throws<ArgumentException>(() => VoxelMaterialPresentationInstaller.Apply(duplicate));
        }
    }
}
