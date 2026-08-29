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
        public IEnumerator Draw_KeepsPackedTopologyStableWithEngineManagedWindClock()
        {
            var batch = new ProceduralGrassBatch();
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
                Assert.That(material.HasProperty("_GrassTime"), Is.False,
                    "Grass wind must use Unity's engine-managed shader clock, not a custom material time uniform.");

                batch.Draw(material);
                yield return null;
                batch.Draw(material);

                Assert.That(batch.ChunkCount, Is.EqualTo(chunkCount));
                Assert.That(batch.BladeCount, Is.EqualTo(bladeCount));
                Assert.That(batch.VertexCount, Is.EqualTo(vertexCount));
                Assert.That(batch.TriangleCount, Is.EqualTo(triangleCount),
                    "Wind animation must remain GPU deformation; drawing across frames must not rebuild packed grass meshes.");
            }
            finally
            {
                batch.Dispose();
                if (material != null) Object.Destroy(material);
            }
        }
    }
}
