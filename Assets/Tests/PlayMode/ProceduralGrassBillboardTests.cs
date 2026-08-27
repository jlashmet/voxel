using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ProceduralGrassBillboardTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator GrassSilhouetteRemainsReadableAcrossCameraAzimuths()
        {
            Shader shader = Shader.Find(ProceduralVegetationMaterials.FoliageShaderName);
            Assert.That(shader, Is.Not.Null, "Production foliage shader must be available in PlayMode.");

            var cameraObject = new GameObject("Grass billboard regression camera");
            var bladeObject = new GameObject("Grass billboard regression blade");
            var camera = cameraObject.AddComponent<Camera>();
            var filter = bladeObject.AddComponent<MeshFilter>();
            var renderer = bladeObject.AddComponent<MeshRenderer>();
            var mesh = BuildQuad();
            var material = new Material(shader) { enableInstancing = true };
            var block = new MaterialPropertyBlock();
            var target = new RenderTexture(160, 160, 24, RenderTextureFormat.ARGB32);
            var front = new Texture2D(160, 160, TextureFormat.RGBA32, false);
            var side = new Texture2D(160, 160, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                ProceduralVegetationMaterials.Configure(block, VegetationKind.Grass);
                block.SetFloat("_WindStrength", 0f);
                renderer.SetPropertyBlock(block);

                material.SetFloat("_UseValidationAnimationTime", 1f);
                material.SetFloat("_ValidationAnimationTime", 0f);

                target.Create();
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.82f;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 10f;

                SetView(camera.transform, new Vector3(0f, 0.52f, -3f));
                yield return null;
                yield return new WaitForEndOfFrame();
                Read(target, front);

                SetView(camera.transform, new Vector3(3f, 0.52f, 0f));
                yield return null;
                yield return new WaitForEndOfFrame();
                Read(target, side);

                int frontPixels = CountGrassPixels(front);
                int sidePixels = CountGrassPixels(side);

                Assert.That(frontPixels, Is.GreaterThan(350),
                    $"Front view produced only {frontPixels} readable grass pixels.");
                Assert.That(sidePixels, Is.GreaterThan(frontPixels * 0.72f),
                    $"A camera-facing grass card must retain its silhouette when viewed from the side; "
                    + $"front={frontPixels}, side={sidePixels}.");
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(front);
                Object.Destroy(side);
                Object.Destroy(material);
                Object.Destroy(mesh);
                Object.Destroy(bladeObject);
                Object.Destroy(cameraObject);
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Grass billboard regression quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.42f, 0f, 0f),
                new Vector3( 0.42f, 0f, 0f),
                new Vector3( 0.42f, 1f, 0f),
                new Vector3(-0.42f, 1f, 0f),
            };
            mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SetView(Transform cameraTransform, Vector3 position)
        {
            cameraTransform.position = position;
            cameraTransform.LookAt(new Vector3(0f, 0.52f, 0f), Vector3.up);
        }

        private static void Read(RenderTexture target, Texture2D destination)
        {
            RenderTexture.active = target;
            destination.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            destination.Apply(false, false);
        }

        private static int CountGrassPixels(Texture2D image)
        {
            Color32[] pixels = image.GetPixels32();
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.g > 24 && pixel.g > pixel.r + 4 && pixel.g > pixel.b + 4)
                    count++;
            }
            return count;
        }
    }
}
