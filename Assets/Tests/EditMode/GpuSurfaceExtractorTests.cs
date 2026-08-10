using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>Small, deterministic proof that GPU extraction produces a real smooth mesh.</summary>
    public sealed class GpuSurfaceExtractorTests
    {
        private const string ShaderPath =
            "Assets/VoxelEngine/Rendering/Shaders/SmoothSurface.compute";
        private const string DrawShaderPath =
            "Assets/VoxelEngine/Rendering/Shaders/SmoothSurface.shader";
        private const string BrickShaderPath =
            "Assets/VoxelEngine/Rendering/Shaders/BrickRaymarch.compute";

        [Test]
        public void SphereProducesClosedFractionalSurfaceWithOutwardNormals()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("This graphics device does not support compute shaders.");

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            Assert.NotNull(shader, $"Missing compute shader at {ShaderPath}");

            var grid = new int3(18, 18, 18);
            int sampleCount = grid.x * grid.y * grid.z;
            var density = new float[sampleCount];
            var materials = new uint[sampleCount];
            var centre = new Vector3(8.5f, 8.5f, 8.5f);
            const float radius = 6.15f;

            for (int z = 0; z < grid.z; z++)
            for (int y = 0; y < grid.y; y++)
            for (int x = 0; x < grid.x; x++)
            {
                int index = x + grid.x * (y + grid.y * z);
                float signedDistance = radius - Vector3.Distance(new Vector3(x, y, z), centre);
                density[index] = Mathf.Clamp01(0.5f + signedDistance * 0.45f);
                materials[index] = signedDistance >= 0f ? 1u : 0u;
            }

            using var extractor = new GpuSurfaceExtractor();
            extractor.Extract(shader, density, materials, grid, Vector3.zero, 1f);

            int indexCount = extractor.ReadIndexCount();
            Assert.Greater(indexCount, 600, "The sphere should contain many connected triangles.");
            Assert.Zero(indexCount % 6, "Surface Nets emits complete two-triangle quads.");
            Assert.LessOrEqual(indexCount, extractor.MaxIndexCount);

            var vertices = new SmoothSurfaceVertex[extractor.CellCount];
            extractor.VertexBuffer.GetData(vertices);
            var indices = new uint[indexCount];
            extractor.IndexBuffer.GetData(indices, 0, 0, indexCount);

            int fractionalVertices = 0;
            int referencedVertices = 0;
            float worstRadiusError = 0f;
            for (int i = 0; i < indices.Length; i++)
            {
                int vertexIndex = checked((int)indices[i]);
                Assert.That(vertexIndex, Is.InRange(0, vertices.Length - 1));
                SmoothSurfaceVertex vertex = vertices[vertexIndex];
                // Bit 0 is the active flag; bits 8..15 carry extraction-time occlusion.
                Assert.AreEqual(1u, vertex.Active & 1u,
                    "Every emitted index must reference an active cell.");
                Assert.AreEqual(1u, vertex.Material, "The surface should inherit the enclosed material.");

                if (i % 6 != 0) continue; // sample one corner per quad; duplicates do not skew QA
                referencedVertices++;
                Vector3 fromCentre = vertex.Position - centre;
                worstRadiusError = Mathf.Max(worstRadiusError, Mathf.Abs(fromCentre.magnitude - radius));
                Assert.That(vertex.Normal.magnitude, Is.EqualTo(1f).Within(0.015f));
                Assert.Greater(Vector3.Dot(vertex.Normal, fromCentre.normalized), 0.72f,
                    "Density gradients must face out of the enclosed volume.");

                Vector3 rounded = new Vector3(Mathf.Round(vertex.Position.x),
                                              Mathf.Round(vertex.Position.y),
                                              Mathf.Round(vertex.Position.z));
                if ((vertex.Position - rounded).sqrMagnitude > 0.0025f) fractionalVertices++;
            }

            Assert.Greater(fractionalVertices, referencedVertices * 3 / 4,
                "The surface must use interpolated positions rather than voxel-face snapping.");
            Assert.Less(worstRadiusError, 0.65f,
                "The extracted geometry should closely follow the source scalar sphere.");

            AssertConsistentTriangleWinding(vertices, indices);

            AssertSmoothRenderedSilhouette(extractor, centre);
        }

        /// <summary>
        /// Every triangle must wind the same way round its own normal.
        ///
        /// The three lattice axes each emit quads from their own corner ordering, and the flip
        /// condition has to be chosen per axis to match. It was not: X and Z wound opposite to Y,
        /// so walls faced inward while ground faced outward. Cull Off hid it until backfaces
        /// showed through as flat shelves with normals pointing in arbitrary directions, which is
        /// what made the surface fall apart under a moving camera. Checking that normals point
        /// outward is not enough — these normals always did.
        /// </summary>
        private static void AssertConsistentTriangleWinding(SmoothSurfaceVertex[] vertices,
                                                            uint[] indices)
        {
            int agreeing = 0;
            int triangles = 0;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]].Position;
                Vector3 b = vertices[indices[i + 1]].Position;
                Vector3 c = vertices[indices[i + 2]].Position;
                Vector3 geometric = Vector3.Cross(b - a, c - a);
                if (geometric.sqrMagnitude < 1e-12f) continue; // degenerate quad corner

                Vector3 shading = vertices[indices[i]].Normal
                                + vertices[indices[i + 1]].Normal
                                + vertices[indices[i + 2]].Normal;
                triangles++;
                if (Vector3.Dot(geometric, shading) > 0f) agreeing++;
            }

            Assert.Greater(triangles, 100, "The sphere should produce many non-degenerate faces.");
            Assert.AreEqual(triangles, agreeing,
                $"All {triangles} triangles must wind consistently with their vertex normals; " +
                $"{triangles - agreeing} disagreed, which is a backfacing surface.");
        }

        [Test]
        public void SparseBrickMirrorFeedsGpuSurfaceWithoutCpuDensityReadback()
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("This graphics device does not support compute shaders.");

            ComputeShader surfaceShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ShaderPath);
            ComputeShader densityShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(BrickShaderPath);
            Assert.NotNull(surfaceShader);
            Assert.NotNull(densityShader);

            var pool = new BrickPool(64, Allocator.Persistent);
            var table = new RegionTable(1, Allocator.Persistent);
            using var mirror = new VoxelGpuBuffers();
            using var extractor = new GpuSurfaceExtractor();

            try
            {
                Region region = table.LoadRegion(int3.zero);
                var centre = new Vector3(8.5f, 8.5f, 8.5f);
                const float radius = 6.15f;

                // Three bricks per axis cover the 18^3 sparse lattice, including the halo brick.
                for (int bz = 0; bz < 3; bz++)
                for (int by = 0; by < 3; by++)
                for (int bx = 0; bx < 3; bx++)
                {
                    int poolIndex = pool.Allocate();
                    region.SetBrick(bx, by, bz, BrickRef.FromPoolIndex(poolIndex));
                    for (int z = 0; z < 8; z++)
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                    {
                        Vector3 world = new Vector3(bx * 8 + x, by * 8 + y, bz * 8 + z);
                        if (Vector3.Distance(world, centre) <= radius)
                            pool.SetVoxel(poolIndex, x | (y << 3) | (z << 6), 1);
                    }
                }

                table.CommitRegion(region);
                mirror.Sync(ref table, ref pool, int3.zero,
                            new HashSet<int3> { int3.zero });
                Assert.AreEqual(27, mirror.DensityJobCount);
                DispatchDensity(densityShader, mirror);

                extractor.ExtractSparse(surfaceShader, mirror, new int3(18), int3.zero, 1f);
                int indexCount = extractor.ReadIndexCount();
                Assert.Greater(indexCount, 500,
                    "The live sparse buffers should produce connected surface topology.");

                var vertices = new SmoothSurfaceVertex[extractor.CellCount];
                extractor.VertexBuffer.GetData(vertices);
                int active = 0;
                int fractional = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].Active == 0u) continue;
                    active++;
                    Vector3 p = vertices[i].Position;
                    Vector3 rounded = new(Mathf.Round(p.x), Mathf.Round(p.y), Mathf.Round(p.z));
                    if ((p - rounded).sqrMagnitude > 0.0025f) fractional++;
                }
                Assert.Greater(active, 100);
                Assert.Greater(fractional, active * 2 / 3,
                    "Sparse extraction must retain fractional dual vertices.");

                // Production chunks use bounded arena slices rather than allocating the strict
                // worst-case topology buffer. A deliberately undersized slice must remain a
                // valid indirect draw: a completely written prefix of whole quads, never an
                // out-of-bounds count or a partially emitted triangle pair.
                const int boundedIndexCapacity = 240;
                extractor.ExtractSparse(surfaceShader, mirror, new int3(18), int3.zero, 1f,
                                        maxIndexCount: boundedIndexCapacity);
                int boundedCount = extractor.ReadIndexCount();
                Assert.AreEqual(boundedIndexCapacity, boundedCount,
                    "Overflow should clamp the indirect draw to the allocated arena slice.");
                Assert.Zero(boundedCount % 6,
                    "A bounded extraction must expose only complete Surface Nets quads.");
                Assert.AreEqual(boundedIndexCapacity, extractor.MaxIndexCount);

                var boundedIndices = new uint[boundedCount];
                extractor.IndexBuffer.GetData(boundedIndices);
                for (int i = 0; i < boundedIndices.Length; i++)
                    Assert.That(boundedIndices[i], Is.LessThan((uint)extractor.CellCount));

                // Match the render-graph production route: extraction is recorded, then executed
                // as part of one ordered GPU command stream rather than dispatched immediately.
                using (var recorded = new CommandBuffer { name = "Recorded sparse extraction" })
                {
                    extractor.RecordExtractSparse(recorded, surfaceShader, mirror, new int3(18),
                                                  int3.zero, 1f, maxIndexCount: 300);
                    Graphics.ExecuteCommandBuffer(recorded);
                }
                Assert.AreEqual(300, extractor.ReadIndexCount());
                Assert.AreEqual(300, extractor.MaxIndexCount);
            }
            finally
            {
                table.Dispose();
                pool.Dispose();
            }
        }

        private static void DispatchDensity(ComputeShader shader, VoxelGpuBuffers buffers)
        {
            int kernel = shader.FindKernel("CSBuildDensity");
            shader.SetBuffer(kernel, "g_RegionWindow", buffers.WindowBuffer);
            shader.SetBuffer(kernel, "g_BrickRefs", buffers.BrickRefBuffer);
            shader.SetBuffer(kernel, "g_BrickVoxels", buffers.VoxelBuffer);
            shader.SetBuffer(kernel, "g_BrickDensity", buffers.DensityBuffer);
            shader.SetBuffer(kernel, "g_DensityJobs", buffers.DensityJobBuffer);
            shader.SetInt("g_DensityJobCount", buffers.DensityJobCount);
            shader.SetInt("g_WindowX", VoxelGpuBuffers.WindowX);
            shader.SetInt("g_WindowY", VoxelGpuBuffers.WindowY);
            shader.SetInt("g_WindowZ", VoxelGpuBuffers.WindowZ);
            int3 origin = buffers.WindowOrigin;
            shader.SetVector("g_WindowOrigin", new Vector4(origin.x, origin.y, origin.z, 0));
            shader.Dispatch(kernel, buffers.DensityJobCount, 1, 1);
        }

        private static void AssertSmoothRenderedSilhouette(GpuSurfaceExtractor extractor,
                                                           Vector3 centre)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(DrawShaderPath);
            Assert.NotNull(shader, $"Missing draw shader at {DrawShaderPath}");

            var material = new Material(shader);
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
            {
                name = "GpuSmoothSurfaceTest"
            };
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var command = new CommandBuffer { name = "Render smooth GPU sphere" };

            try
            {
                target.Create();
                material.SetColor("_BaseColor", new Color(0.32f, 0.82f, 0.48f, 1f));
                Vector3 cameraPosition = centre + new Vector3(0f, 0f, -24f);
                Quaternion rotation = Quaternion.LookRotation(centre - cameraPosition, Vector3.up);
                // Unity cameras look down local +Z, while the graphics view convention expects
                // visible points at negative camera Z. Camera.worldToCameraMatrix supplies this
                // handedness flip; the manual fixture must do the same.
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f))
                               * Matrix4x4.TRS(cameraPosition, rotation, Vector3.one).inverse;
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(
                    Matrix4x4.Perspective(36f, 1f, 0.1f, 100f), true);

                command.SetRenderTarget(target);
                command.ClearRenderTarget(true, true, Color.black);
                command.SetViewProjectionMatrices(view, projection);
                extractor.Draw(command, material);
                Graphics.ExecuteCommandBuffer(command);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes("/tmp/gpu_smooth_surface.png", pixels.EncodeToPNG());

                Color32[] colours = pixels.GetPixels32();
                int coveredPixels = 0;
                var uniqueWidths = new System.Collections.Generic.HashSet<int>();
                for (int y = 0; y < 256; y++)
                {
                    int first = 256;
                    int last = -1;
                    for (int x = 0; x < 256; x++)
                    {
                        Color32 colour = colours[x + y * 256];
                        if (colour.r < 10 && colour.g < 10 && colour.b < 10) continue;
                        coveredPixels++;
                        first = Mathf.Min(first, x);
                        last = Mathf.Max(last, x);
                    }

                    if (last >= first) uniqueWidths.Add(last - first + 1);
                }

                Assert.Greater(coveredPixels, 6000, "The indirect GPU draw should render the sphere.");
                Assert.Greater(uniqueWidths.Count, 28,
                    "A smooth sphere silhouette should vary continuously across scanlines, not in voxel tiers.");
            }
            finally
            {
                command.Release();
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(material);
            }
        }
    }
}
