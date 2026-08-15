using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.CI
{
    internal static partial class KentridgeUnifiedCaptureV2
    {
        private static void ConfigureCamera(Camera camera, View view, Vector3 focus,
                                            float span, float distance, int centreX,
                                            int centreZ, float centreY)
        {
            if (!view.Street)
            {
                camera.fieldOfView = 39f;
                camera.transform.position = focus + view.Direction * distance + Vector3.up * (distance * 0.62f);
                camera.transform.LookAt(focus);
                camera.farClipPlane = distance * 3.5f;
                return;
            }

            float d = Mathf.Max(64f, span * 0.54f);
            Vector3 offset = view.Direction * d;
            int x = centreX + Mathf.RoundToInt(offset.x / VoxelSize);
            int z = centreZ + Mathf.RoundToInt(offset.z / VoxelSize);
            camera.fieldOfView = 52f;
            camera.transform.position = new Vector3(
                focus.x + offset.x,
                SurfaceY(x, z) + 4.2f,
                focus.z + offset.z);
            camera.transform.LookAt(new Vector3(focus.x, centreY + 5.2f, focus.z));
            camera.farClipPlane = Mathf.Max(240f, span * 2.2f);
        }

        private static Mesh BuildMesh(CpuTransvoxelChunkCache.Entry entry,
                                      out int triangleCount, out int architecturalCount)
        {
            var sourceVertices = new SmoothSurfaceVertex[entry.Vertices.count];
            var sourceIndices = new uint[entry.IndexCount];
            entry.Vertices.GetData(sourceVertices);
            entry.Indices.GetData(sourceIndices, 0, 0, entry.IndexCount);
            var vertices = new Vector3[sourceVertices.Length];
            var normals = new Vector3[sourceVertices.Length];
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                vertices[i] = sourceVertices[i].Position;
                normals[i] = sourceVertices[i].Normal;
            }

            var groups = new List<int>[PresentationCount];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<int>();
            architecturalCount = 0;
            for (int i = 0; i + 2 < sourceIndices.Length; i += 3)
            {
                int first = (int)sourceIndices[i];
                uint packed = sourceVertices[first].Material;
                int material = (int)(packed & 0xFFu);
                ushort style = (ushort)((packed >> 16) & 0xFFu);
                if ((uint)material >= MaterialCount) material = 1;
                bool architectural =
                    style != SurfaceStyles.MaterialDefault && style != SurfaceStyles.Smooth;
                int group = material + (architectural ? MaterialCount : 0);
                if (architectural) architecturalCount++;
                groups[group].Add((int)sourceIndices[i]);
                groups[group].Add((int)sourceIndices[i + 1]);
                groups[group].Add((int)sourceIndices[i + 2]);
            }

            var mesh = new Mesh
            {
                name = $"CI Kentridge Unified V2 {entry.Coordinate}",
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                normals = normals,
                subMeshCount = PresentationCount,
            };
            for (int i = 0; i < groups.Length; i++) mesh.SetTriangles(groups[i], i, false);
            mesh.RecalculateBounds();
            triangleCount = sourceIndices.Length / 3;
            return mesh;
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D image, string path)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static Shader FindPreviewShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            return shader != null ? shader : throw new InvalidOperationException("No CI preview shader found.");
        }

        private static Material[] BuildPalette(Shader shader)
        {
            Color[] smooth = BaseColours();
            smooth[1] = new Color(0.25f, 0.48f, 0.20f, 1f);
            smooth[3] = new Color(0.70f, 0.62f, 0.44f, 1f);
            smooth[5] = new Color(0.23f, 0.20f, 0.18f, 1f);
            smooth[10] = new Color(0.24f, 0.54f, 0.18f, 1f);
            smooth[13] = new Color(0.38f, 0.25f, 0.13f, 1f);
            smooth[14] = new Color(0.16f, 0.42f, 0.13f, 1f);

            Color[] hard = BaseColours();
            hard[1] = new Color(0.68f, 0.64f, 0.55f, 1f);
            hard[2] = new Color(0.30f, 0.15f, 0.06f, 1f);
            hard[4] = new Color(0.30f, 0.70f, 0.86f, 1f);
            hard[6] = new Color(0.24f, 0.25f, 0.28f, 1f);
            hard[7] = new Color(0.20f, 0.26f, 0.36f, 1f);
            hard[8] = new Color(0.66f, 0.20f, 0.10f, 1f);
            hard[9] = new Color(0.52f, 0.16f, 0.61f, 1f);
            hard[15] = new Color(1.00f, 0.63f, 0.12f, 1f);

            var result = new Material[PresentationCount];
            for (int i = 0; i < MaterialCount; i++)
                result[i] = NewMaterial(shader, $"CI Kentridge Smooth {i}", smooth[i]);
            for (int i = 0; i < MaterialCount; i++)
                result[MaterialCount + i] = NewMaterial(
                    shader, $"CI Kentridge Architectural {i}", hard[i]);
            return result;
        }

        private static Color[] BaseColours()
        {
            var colours = new Color[MaterialCount];
            for (int i = 0; i < colours.Length; i++)
                colours[i] = new Color(0.55f, 0.55f, 0.55f, 1f);
            return colours;
        }

        private static Material NewMaterial(Shader shader, string name, Color colour)
        {
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            return material;
        }

        private static void DestroyPalette(Material[] palette)
        {
            if (palette == null) return;
            for (int i = 0; i < palette.Length; i++)
                if (palette[i] != null) UnityEngine.Object.DestroyImmediate(palette[i]);
        }
    }
}
