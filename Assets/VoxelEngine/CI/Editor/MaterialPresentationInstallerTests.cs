using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;

namespace VoxelEngine.CI
{
    public sealed class MaterialPresentationInstallerTests
    {
        [Test]
        public void Apply_UsesOpaqueRowsAndNeutralizesUnassignedRows()
        {
            Vector4[] albedo = (Vector4[])VoxelPresentationCatalogue.MaterialAlbedo.Clone();
            Vector4[] sampling = (Vector4[])VoxelPresentationCatalogue.MaterialSampling.Clone();
            Vector4[] surface = (Vector4[])VoxelPresentationCatalogue.MaterialSurface.Clone();
            Vector4[] variation = (Vector4[])VoxelPresentationCatalogue.MaterialVariation.Clone();

            try
            {
                var definitions = new[]
                {
                    new MaterialPresentationDefinition(2, new float4(0.2f, 0.3f, 0.4f, 1f)),
                    new MaterialPresentationDefinition(7, new float4(0.7f, 0.6f, 0.5f, 1f)),
                };

                VoxelMaterialPresentationInstaller.Apply(definitions);

                Assert.That(VoxelPresentationCatalogue.MaterialAlbedo[2],
                    Is.EqualTo(new Vector4(0.2f, 0.3f, 0.4f, 1f)));
                Assert.That(VoxelPresentationCatalogue.MaterialAlbedo[7],
                    Is.EqualTo(new Vector4(0.7f, 0.6f, 0.5f, 1f)));

                int unassignedRow = VoxelPresentationCatalogue.MaxMaterials - 1;
                Assert.That(VoxelPresentationCatalogue.MaterialAlbedo[unassignedRow],
                    Is.EqualTo(new Vector4(1f, 1f, 1f, 1f)));
                Assert.That(VoxelPresentationCatalogue.MaterialSampling[unassignedRow],
                    Is.EqualTo(Vector4.zero));
            }
            finally
            {
                Array.Copy(albedo, VoxelPresentationCatalogue.MaterialAlbedo, albedo.Length);
                Array.Copy(sampling, VoxelPresentationCatalogue.MaterialSampling, sampling.Length);
                Array.Copy(surface, VoxelPresentationCatalogue.MaterialSurface, surface.Length);
                Array.Copy(variation, VoxelPresentationCatalogue.MaterialVariation, variation.Length);
            }
        }

        [Test]
        public void Apply_RejectsDuplicateOpaqueMaterialIndicesWithoutMutatingRows()
        {
            Vector4[] before = (Vector4[])VoxelPresentationCatalogue.MaterialAlbedo.Clone();
            var duplicate = new[]
            {
                new MaterialPresentationDefinition(3, new float4(1f, 0f, 0f, 1f)),
                new MaterialPresentationDefinition(3, new float4(0f, 1f, 0f, 1f)),
            };

            Assert.Throws<ArgumentException>(() =>
                VoxelMaterialPresentationInstaller.Apply(duplicate));

            for (int i = 0; i < before.Length; i++)
                Assert.That(VoxelPresentationCatalogue.MaterialAlbedo[i], Is.EqualTo(before[i]));
        }
    }
}
