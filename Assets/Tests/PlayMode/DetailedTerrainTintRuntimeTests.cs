using System.Collections;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// GPU regression for SceneIssues/20260826-132144-249-VoxelShowcase.
    /// The reported seam was caused by SmoothSurface blending its final detailed-terrain colour
    /// toward the sky as camera distance increased. This test executes that production shader with
    /// identical material inputs at near and handoff-range distances and verifies colour stability.
    /// </summary>
    public sealed class DetailedTerrainTintRuntimeTests
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SurfaceVertex
        {
            public Vector3 Position;
            public Vector3 Normal;
            public uint Material;
            public uint Active;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SurfaceDrawMetadata
        {
            public uint IndexStart;
            public uint VertexStart;
            public uint IndexCount;
            public uint Padding;
        }

        [UnityTest]
        [Category("Rendering")]
        public IEnumerator DetailedSurfaceColourDoesNotShiftTowardSkyWithDistance()
        {
            Shader shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader");
            Assert.IsNotNull(shader, "SmoothSurface production shader could not be loaded.");

            using var fixture = new SmoothSurfaceGpuFixture(shader);
            yield return null;

            Color near = fixture.Render(20f);
            Color handoffRange = fixture.Render(220f);

            Assert.Greater(near.g, near.b + 0.08f,
                $"Control surface was not material-green at 20 m: {near}.");
            Assert.Greater(handoffRange.g, handoffRange.b + 0.08f,
                $"Detailed surface acquired a blue cast by 220 m: {handoffRange}.");

            var nearRgb = new Vector3(near.r, near.g, near.b);
            var farRgb = new Vector3(handoffRange.r, handoffRange.g, handoffRange.b);
            float colourDrift = Vector3.Distance(nearRgb, farRgb);
            Assert.Less(colourDrift, 0.035f,
                $"Identical detailed material shifted by {colourDrift:0.000} between 20 m "
              + $"({near}) and 220 m ({handoffRange}). Camera distance must not blend the "
              + "high-detail surface toward the blue sky.");
        }

        private sealed class SmoothSurfaceGpuFixture : System.IDisposable
        {
            private readonly GameObject _cameraObject;
            private readonly GameObject _surfaceObject;
            private readonly Camera _camera;
            private readonly Mesh _mesh;
            private readonly Material _material;
            private readonly Texture2DArray _albedoTextures;
            private readonly Texture2DArray _normalTextures;
            private readonly ComputeBuffer _vertices;
            private readonly ComputeBuffer _indices;
            private readonly ComputeBuffer _metadata;
            private readonly RenderTexture _target;

            public SmoothSurfaceGpuFixture(Shader shader)
            {
                _material = new Material(shader);
                ConfigureMaterial(_material, out _albedoTextures, out _normalTextures);

                _vertices = new ComputeBuffer(3, Marshal.SizeOf<SurfaceVertex>());
                _indices = new ComputeBuffer(6, sizeof(uint));
                _metadata = new ComputeBuffer(1, Marshal.SizeOf<SurfaceDrawMetadata>());
                _indices.SetData(new uint[] { 0, 1, 2, 2, 1, 0 });
                _metadata.SetData(new[]
                {
                    new SurfaceDrawMetadata
                    {
                        IndexStart = 0,
                        VertexStart = 0,
                        IndexCount = 6,
                        Padding = 0
                    }
                });
                _material.SetBuffer("_SurfaceVertices", _vertices);
                _material.SetBuffer("_SurfaceIndices", _indices);
                _material.SetBuffer("_SurfaceDrawMetadata", _metadata);
                _material.SetInt("_SurfaceDrawBase", 0);

                _mesh = new Mesh { name = "DetailedTerrainTintRuntimeTriangle" };
                _mesh.vertices = new[]
                {
                    Vector3.forward * 100f, Vector3.forward * 100f, Vector3.forward * 100f,
                    Vector3.forward * 100f, Vector3.forward * 100f, Vector3.forward * 100f
                };
                _mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
                _mesh.bounds = new Bounds(new Vector3(0f, 0f, 200f),
                                          new Vector3(20f, 20f, 600f));

                _surfaceObject = new GameObject("DetailedTerrainTintRuntimeSurface");
                _surfaceObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
                _surfaceObject.AddComponent<MeshRenderer>().sharedMaterial = _material;

                _target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32)
                {
                    name = "DetailedTerrainTintRuntimeTarget"
                };
                _target.Create();

                _cameraObject = new GameObject("DetailedTerrainTintRuntimeCamera");
                _camera = _cameraObject.AddComponent<Camera>();
                _camera.orthographic = true;
                _camera.orthographicSize = 1.5f;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 500f;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = Color.black;
                _camera.targetTexture = _target;
            }

            public Color Render(float distance)
            {
                _vertices.SetData(new[]
                {
                    new SurfaceVertex
                    {
                        Position = new Vector3(-1.2f, -1.2f, distance),
                        Normal = Vector3.up,
                        Material = 0,
                        Active = 0x0000FF00u
                    },
                    new SurfaceVertex
                    {
                        Position = new Vector3(1.2f, -1.2f, distance),
                        Normal = Vector3.up,
                        Material = 0,
                        Active = 0x0000FF00u
                    },
                    new SurfaceVertex
                    {
                        Position = new Vector3(0f, 1.2f, distance),
                        Normal = Vector3.up,
                        Material = 0,
                        Active = 0x0000FF00u
                    }
                });

                _camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = _target;
                var shot = new Texture2D(64, 64, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                shot.Apply();
                Color pixel = shot.GetPixel(32, 30);
                Object.DestroyImmediate(shot);
                RenderTexture.active = previous;
                return pixel;
            }

            public void Dispose()
            {
                if (_camera != null)
                    _camera.targetTexture = null;
                _vertices?.Release();
                _indices?.Release();
                _metadata?.Release();
                if (_target != null)
                {
                    _target.Release();
                    Object.DestroyImmediate(_target);
                }
                Object.DestroyImmediate(_cameraObject);
                Object.DestroyImmediate(_surfaceObject);
                Object.DestroyImmediate(_mesh);
                Object.DestroyImmediate(_material);
                Object.DestroyImmediate(_albedoTextures);
                Object.DestroyImmediate(_normalTextures);
            }

            private static void ConfigureMaterial(
                Material material,
                out Texture2DArray albedoTextures,
                out Texture2DArray normalTextures)
            {
                albedoTextures = MakeTextureArray(Color.white);
                normalTextures = MakeTextureArray(new Color(0.5f, 0.5f, 1f, 1f));
                material.SetTexture("_AlbedoTextures", albedoTextures);
                material.SetTexture("_NormalTextures", normalTextures);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_VoxelSize", 1f);
                material.SetFloat("_DebugCoverage", 0f);
                material.SetVector("_SunDirection", new Vector4(0f, 1f, 0f, 0f));
                material.SetVector("_SkyHorizon", new Vector4(0.05f, 0.18f, 0.95f, 1f));
                material.SetVector("_SkyZenith", new Vector4(0.02f, 0.12f, 1f, 1f));

                var materialAlbedo = new Vector4[32];
                materialAlbedo[0] = new Vector4(0.12f, 0.68f, 0.12f, 1f);
                material.SetVectorArray("_MaterialAlbedo", materialAlbedo);

                var materialSampling = new Vector4[32];
                material.SetVectorArray("_MaterialSampling", materialSampling);

                var materialSurface = new Vector4[32];
                materialSurface[0] = new Vector4(1f, 0f, 1f, 0f);
                material.SetVectorArray("_MaterialSurface", materialSurface);

                var materialVariation = new Vector4[32];
                materialVariation[0] = new Vector4(1f, 0f, 0f, 0f);
                material.SetVectorArray("_MaterialVariation", materialVariation);

                material.SetVectorArray("_CoatingTint", new Vector4[16]);
                material.SetVectorArray("_CoatingSampling", new Vector4[16]);
                material.SetVectorArray("_CoatingResponse", new Vector4[16]);
                material.SetVectorArray("_SurfacePattern", new Vector4[32]);
                material.SetVectorArray("_SurfaceJointColour", new Vector4[32]);

                var detailResponse = new Vector4[32];
                detailResponse[0] = new Vector4(0f, 1f, 0f, 0.5f);
                material.SetVectorArray("_SurfaceDetailResponse", detailResponse);
            }

            private static Texture2DArray MakeTextureArray(Color colour)
            {
                var texture = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { colour }, 0);
                texture.Apply(false, false);
                return texture;
            }
        }
    }
}
