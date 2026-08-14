using System;
using System.Collections.Generic;
using System.IO;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Core.Terrain;
using VoxelEngine.Rendering.SurfaceExtraction;

namespace VoxelEngine.CI
{
    internal static class KentridgeCaptureImpl
    {
        private const uint Seed = 0x4B454E54u;
        private const float VoxelSize = 0.1f;
        private const int Width = 1600;
        private const int Height = 1000;
        private const int MaterialCount = 18;

        private readonly struct View
        {
            public readonly string Name;
            public readonly Vector3 Direction;
            public readonly bool Street;
            public View(string name, Vector3 direction, bool street)
            {
                Name = name;
                Direction = direction.normalized;
                Street = street;
            }
        }

        private static readonly View[] Views =
        {
            new("overview-ne", new Vector3( 1, 0,  1), false),
            new("overview-nw", new Vector3(-1, 0,  1), false),
            new("overview-se", new Vector3( 1, 0, -1), false),
            new("overview-sw", new Vector3(-1, 0, -1), false),
            new("street-north", new Vector3( 0, 0,  1), true),
            new("street-south", new Vector3( 0, 0, -1), true),
            new("street-east",  new Vector3( 1, 0,  0), true),
            new("street-west",  new Vector3(-1, 0,  0), true),
        };

        public static void Run()
        {
            string root = Directory.GetParent(Application.dataPath)!.FullName;
            string output = Path.Combine(root, "Artifacts", "Kentridge");
            Directory.CreateDirectory(output);

            FeatureCatalogue catalogue = default;
            RegionTable table = default;
            BrickPool pool = default;
            CpuTransvoxelChunkCache cache = null;
            GameObject cameraObject = null;
            GameObject terrainObject = null;
            Mesh terrainMesh = null;
            Material terrainMaterial = null;
            Material[] palette = null;
            RenderTexture target = null;
            Texture2D image = null;
            var objects = new List<GameObject>();
            var meshes = new List<Mesh>();

            try
            {
                SettlementPlan plan = KentridgeDefinition.Build(Seed);
                if (plan.Plots.Count != 17 || plan.Streets.Count == 0)
                    throw new InvalidOperationException("Kentridge settlement contract changed.");

                TownBounds(plan, out int minX, out int maxX, out int minZ, out int maxZ);
                catalogue = KentridgeCombinedVoxelCatalogue.Build(
                    Seed, BuildSettings(), Allocator.Persistent);
                table = new RegionTable(64, Allocator.Persistent);
                pool = new BrickPool(262144, Allocator.Persistent);

                int featureInstances = 0;
                int featureVoxels = 0;
                int minRX = minX >> VoxelDimensions.RegionVoxelEdgeLog2;
                int maxRX = maxX >> VoxelDimensions.RegionVoxelEdgeLog2;
                int minRZ = minZ >> VoxelDimensions.RegionVoxelEdgeLog2;
                int maxRZ = maxZ >> VoxelDimensions.RegionVoxelEdgeLog2;
                for (int rz = minRZ; rz <= maxRZ; rz++)
                for (int rx = minRX; rx <= maxRX; rx++)
                {
                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                        in catalogue, Seed, new int3(rx, 0, rz), ref table, ref pool);
                    if (report.BudgetExceeded)
                        throw new InvalidOperationException($"Kentridge feature budget exceeded in {rx},{rz}.");
                    featureInstances += report.InstancesRasterised;
                    featureVoxels += report.VoxelsWritten;
                }
                if (featureInstances == 0 || featureVoxels == 0)
                    throw new InvalidOperationException("Kentridge generated no isolated voxel geometry.");

                cameraObject = new GameObject("CI Kentridge Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.68f, 0.82f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                int centreVX = (minX + maxX) / 2;
                int centreVZ = (minZ + maxZ) / 2;
                float centreY = TerrainSampler.HeightAt(centreVX, centreVZ, Seed) * VoxelSize;
                Vector3 focus = new Vector3(centreVX * VoxelSize, centreY + 10f, centreVZ * VoxelSize);
                float span = Mathf.Max(maxX - minX, maxZ - minZ) * VoxelSize;
                float distance = Mathf.Max(120f, span * 1.32f);
                camera.fieldOfView = 55f;
                camera.transform.position = focus + new Vector3(0, distance * 1.15f, -distance * 0.12f);
                camera.transform.LookAt(focus);
                camera.farClipPlane = distance * 4f;

                Shader shader = FindPreviewShader();
                terrainMesh = BuildTerrainMesh(minX, maxX, minZ, maxZ);
                terrainObject = new GameObject("CI Kentridge Terrain");
                terrainObject.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
                terrainMaterial = NewMaterial(shader, "CI Kentridge Terrain", new Color(0.24f, 0.43f, 0.19f, 1f));
                terrainObject.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
                palette = BuildPalette(shader);

                MaterialPalette materialPalette = BuildMaterialPalette();
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                cache = new CpuTransvoxelChunkCache
                {
                    MaxResidentChunks = 16384,
                    MaxViewDistanceMetres = 10000f,
                };
                cache.InvalidateSurfaceBricks(SurfaceChunkSeeds(minX, maxX, minZ, maxZ));

                int previousDirty = int.MaxValue;
                int stalled = 0;
                for (int iteration = 0; iteration < 65536 && cache.DirtyCount > 0; iteration++)
                {
                    cache.Prepare(ref table, in pool, in materialPalette,
                        in surfaces, in coatings, null, camera, VoxelSize, 1, 100.0);
                    int dirty = cache.DirtyCount;
                    if (dirty == previousDirty)
                    {
                        stalled++;
                        if ((stalled & 7) == 0) System.Threading.Thread.Sleep(1);
                    }
                    else
                    {
                        previousDirty = dirty;
                        stalled = 0;
                    }
                }
                if (cache.DirtyCount != 0)
                    throw new InvalidOperationException(
                        $"Unified surface extraction did not settle; {cache.DirtyCount} chunks remain.");

                IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                    cache.CollectVisible(camera, VoxelSize, 1);
                if (visible.Count == 0)
                    throw new InvalidOperationException("Kentridge produced no visible surface chunks.");

                int triangles = 0;
                for (int i = 0; i < visible.Count; i++)
                {
                    Mesh mesh = BuildMesh(visible[i], out int count);
                    triangles += count;
                    var rootObject = new GameObject($"CI Kentridge Chunk {visible[i].Coordinate}");
                    rootObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    rootObject.AddComponent<MeshRenderer>().sharedMaterials = palette;
                    objects.Add(rootObject);
                    meshes.Add(mesh);
                }

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    name = "CI Kentridge Diagnostic Capture",
                };
                target.Create();
                image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                camera.targetTexture = target;

                var metadata = new List<string>();
                for (int i = 0; i < Views.Length; i++)
                {
                    ConfigureCamera(camera, Views[i], focus, span, distance, centreVX, centreVZ, centreY);
                    Capture(camera, target, image, Path.Combine(output, "kentridge-" + Views[i].Name + ".png"));
                    metadata.Add($"view={Views[i].Name} camera={camera.transform.position:F2} " +
                                 $"rotation={camera.transform.eulerAngles:F2} fov={camera.fieldOfView:F1}");
                }

                File.WriteAllText(Path.Combine(output, "kentridge-overview.txt"),
                    $"seed={Seed}\nplots={plan.Plots.Count}\nstreets={plan.Streets.Count}\n" +
                    $"featureInstances={featureInstances}\nfeatureVoxels={featureVoxels}\n" +
                    $"surfaceChunks={visible.Count}\nsurfaceTriangles={triangles}\n" +
                    $"knownChunks={cache.KnownCount}\nresidentChunks={cache.ResidentCount}\n" +
                    $"boundsDm={minX},{minZ}..{maxX},{maxZ}\ncaptures={Views.Length}\n" +
                    string.Join("\n", metadata) + "\n");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                for (int i = 0; i < objects.Count; i++)
                    if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                for (int i = 0; i < meshes.Count; i++)
                    if (meshes[i] != null) UnityEngine.Object.DestroyImmediate(meshes[i]);
                if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
                if (terrainMesh != null) UnityEngine.Object.DestroyImmediate(terrainMesh);
                if (terrainMaterial != null) UnityEngine.Object.DestroyImmediate(terrainMaterial);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (palette != null)
                    for (int i = 0; i < palette.Length; i++)
                        if (palette[i] != null) UnityEngine.Object.DestroyImmediate(palette[i]);
                cache?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        private static void ConfigureCamera(Camera camera, View view, Vector3 focus,
                                            float span, float distance, int centreVX,
                                            int centreVZ, float centreY)
        {
            if (!view.Street)
            {
                camera.fieldOfView = 39f;
                camera.transform.position = focus + view.Direction * distance + Vector3.up * (distance * 0.62f);
                camera.transform.LookAt(focus);
                camera.farClipPlane = distance * 3.5f;
                return;
            }

            float horizontalDistance = Mathf.Max(52f, span * 0.43f);
            Vector3 offset = view.Direction * horizontalDistance;
            int vx = centreVX + Mathf.RoundToInt(offset.x / VoxelSize);
            int vz = centreVZ + Mathf.RoundToInt(offset.z / VoxelSize);
            float terrainY = TerrainSampler.HeightAt(vx, vz, Seed) * VoxelSize;
            camera.fieldOfView = 52f;
            camera.transform.position = new Vector3(focus.x + offset.x, terrainY + 3.4f, focus.z + offset.z);
            camera.transform.LookAt(new Vector3(focus.x, centreY + 5.2f, focus.z));
            camera.farClipPlane = Mathf.Max(240f, span * 2.2f);
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
            finally { RenderTexture.active = previous; }
        }

        private static Mesh BuildMesh(CpuTransvoxelChunkCache.Entry entry, out int triangleCount)
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

            var groups = new List<int>[MaterialCount];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<int>();
            for (int i = 0; i + 2 < sourceIndices.Length; i += 3)
            {
                int first = (int)sourceIndices[i];
                int material = (int)(sourceVertices[first].Material & 0xFFu);
                if ((uint)material >= MaterialCount) material = 1;
                groups[material].Add((int)sourceIndices[i]);
                groups[material].Add((int)sourceIndices[i + 1]);
                groups[material].Add((int)sourceIndices[i + 2]);
            }

            var mesh = new Mesh
            {
                name = $"CI Kentridge {entry.Coordinate}",
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                normals = normals,
                subMeshCount = MaterialCount,
            };
            for (int i = 0; i < MaterialCount; i++) mesh.SetTriangles(groups[i], i, false);
            mesh.RecalculateBounds();
            triangleCount = sourceIndices.Length / 3;
            return mesh;
        }

        private static Mesh BuildTerrainMesh(int minX, int maxX, int minZ, int maxZ)
        {
            const int step = 16;
            int countX = (maxX - minX) / step + 1;
            int countZ = (maxZ - minZ) / step + 1;
            var vertices = new Vector3[countX * countZ];
            var triangles = new int[(countX - 1) * (countZ - 1) * 6];
            for (int z = 0; z < countZ; z++)
            for (int x = 0; x < countX; x++)
            {
                int wx = minX + x * step;
                int wz = minZ + z * step;
                vertices[x + z * countX] = new Vector3(
                    wx * VoxelSize,
                    TerrainSampler.HeightAt(wx, wz, Seed) * VoxelSize - 0.08f,
                    wz * VoxelSize);
            }
            int t = 0;
            for (int z = 0; z < countZ - 1; z++)
            for (int x = 0; x < countX - 1; x++)
            {
                int a = x + z * countX;
                int b = a + 1;
                int c = a + countX;
                int d = c + 1;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
            }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "CI Kentridge Terrain" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<int3> SurfaceChunkSeeds(int minX, int maxX, int minZ, int maxZ)
        {
            int edge = CpuTransvoxelChunkCache.VoxelsPerAxis;
            int minCX = FloorDiv(minX, edge) - 1;
            int maxCX = FloorDiv(maxX, edge) + 1;
            int minCZ = FloorDiv(minZ, edge) - 1;
            int maxCZ = FloorDiv(maxZ, edge) + 1;
            int maxCY = FloorDiv(TerrainSampler.MaxHeight, edge);
            var result = new List<int3>();
            for (int cy = 0; cy <= maxCY; cy++)
            for (int cz = minCZ; cz <= maxCZ; cz++)
            for (int cx = minCX; cx <= maxCX; cx++)
                result.Add(new int3(cx, cy, cz) * CpuTransvoxelChunkCache.BricksPerAxis);
            return result;
        }

        private static MaterialPalette BuildMaterialPalette()
        {
            MaterialPalette result = default;
            for (byte material = 1; material < MaterialCount; material++)
                result.Register(material, 128, DestructionClass.Crumble, SurfaceStyles.Planar, uint.MaxValue);
            return result;
        }

        private static VoxelWorldGenSettings BuildSettings()
        {
            var materials = new VoxelMaterialMap(
                foundationStone: 1, masonry: 1, darkMasonry: 6,
                timber: 2, glass: 4, warmWindow: 15,
                roofTile: 8, slate: 7, cloth: 9,
                moss: 14, water: 11, roadSurface: 13);
            return new VoxelWorldGenSettings(1, materials);
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
            var colours = new Color[MaterialCount];
            for (int i = 0; i < colours.Length; i++) colours[i] = new Color(0.55f, 0.55f, 0.55f, 1f);
            colours[1] = new Color(0.68f, 0.64f, 0.55f, 1f);
            colours[2] = new Color(0.30f, 0.15f, 0.06f, 1f);
            colours[4] = new Color(0.30f, 0.70f, 0.86f, 1f);
            colours[6] = new Color(0.24f, 0.25f, 0.28f, 1f);
            colours[7] = new Color(0.20f, 0.26f, 0.36f, 1f);
            colours[8] = new Color(0.66f, 0.20f, 0.10f, 1f);
            colours[9] = new Color(0.52f, 0.16f, 0.61f, 1f);
            colours[13] = new Color(0.38f, 0.25f, 0.13f, 1f);
            colours[14] = new Color(0.16f, 0.42f, 0.13f, 1f);
            colours[15] = new Color(1.00f, 0.63f, 0.12f, 1f);
            var result = new Material[MaterialCount];
            for (int i = 0; i < result.Length; i++) result[i] = NewMaterial(shader, $"CI Kentridge Material {i}", colours[i]);
            return result;
        }

        private static Material NewMaterial(Shader shader, string name, Color colour)
        {
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.SetColor("_Color", colour);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
            return material;
        }

        private static void TownBounds(SettlementPlan plan,
                                       out int minX, out int maxX, out int minZ, out int maxZ)
        {
            minX = plan.Plaza.CentreDm.X - plan.Plaza.SizeDm.X / 2;
            maxX = plan.Plaza.CentreDm.X + plan.Plaza.SizeDm.X / 2;
            minZ = plan.Plaza.CentreDm.Y - plan.Plaza.SizeDm.Y / 2;
            maxZ = plan.Plaza.CentreDm.Y + plan.Plaza.SizeDm.Y / 2;
            for (int i = 0; i < plan.Streets.Count; i++)
            {
                PlannedStreet street = plan.Streets[i];
                int radius = street.WidthDm / 2;
                for (int p = 0; p < street.Points.Count; p++)
                {
                    Int2 point = street.Points[p];
                    minX = Math.Min(minX, point.X - radius);
                    maxX = Math.Max(maxX, point.X + radius);
                    minZ = Math.Min(minZ, point.Y - radius);
                    maxZ = Math.Max(maxZ, point.Y + radius);
                }
            }
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                minX = Math.Min(minX, plot.PositionDm.X);
                maxX = Math.Max(maxX, plot.PositionDm.X + footprint.X);
                minZ = Math.Min(minZ, plot.PositionDm.Y);
                maxZ = Math.Max(maxZ, plot.PositionDm.Y + footprint.Z);
            }
            minX -= 96;
            maxX += 96;
            minZ -= 96;
            maxZ += 96;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            return value % divisor < 0 ? q - 1 : q;
        }
    }
}
