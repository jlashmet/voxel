using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ProceduralGrassWindTests
    {
        [UnityTest]
        public IEnumerator Draw_PublishesAdvancingWindClockWithoutRebuildingPackedGrass()
        {
            float presentationTime = 10f;
            var batch = new ProceduralGrassBatch(() => presentationTime);
            Material material = null;

            try
            {
                batch.Add(new VegetationInstance
                {
                    PositionMetres = new float3(0f, 0f, 3f),
                    SurfaceNormal = new float3(0f, 1f, 0f),
                    Kind = VegetationKind.Grass,
                    Seed = 12345u,
                    Scale = 1f,
                });
                batch.Rebuild();

                int chunkCount = batch.ChunkCount;
                int bladeCount = batch.BladeCount;
                int vertexCount = batch.VertexCount;
                int triangleCount = batch.TriangleCount;
                Assert.That(chunkCount, Is.GreaterThan(0));
                Assert.That(bladeCount, Is.GreaterThan(0));

                Shader shader = Shader.Find(ProceduralVegetationMaterials.GrassShaderName);
                Assert.That(shader, Is.Not.Null,
                    "The packed-grass shader must be available to the production Draw path.");
                material = new Material(shader);

                batch.Draw(material);
                float firstSubmittedTime = batch.LastSubmittedGrassTime;
                Assert.That(firstSubmittedTime, Is.EqualTo(10f).Within(0.0001f));

                yield return null;

                presentationTime = 11.25f;
                batch.Draw(material);
                float secondSubmittedTime = batch.LastSubmittedGrassTime;

                Assert.That(secondSubmittedTime, Is.EqualTo(11.25f).Within(0.0001f));
                Assert.That(secondSubmittedTime, Is.GreaterThan(firstSubmittedTime),
                    "Packed grass must snapshot a fresh wind clock for each frame's draw submission.");
                Assert.That(batch.ChunkCount, Is.EqualTo(chunkCount));
                Assert.That(batch.BladeCount, Is.EqualTo(bladeCount));
                Assert.That(batch.VertexCount, Is.EqualTo(vertexCount));
                Assert.That(batch.TriangleCount, Is.EqualTo(triangleCount),
                    "Wind animation must remain GPU deformation; advancing time must not rebuild grass meshes.");
            }
            finally
            {
                batch.Dispose();
                if (material != null) Object.Destroy(material);
            }
        }
    }
}
