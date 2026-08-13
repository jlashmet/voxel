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
    /// <summary>
    /// Isolated deterministic visual test for Kentridge. It does not load VoxelShowcase or use
    /// ShowcaseWorld. The real Kentridge catalogue is rasterised into private voxel storage, then
    /// the production hard-surface extractor supplies the geometry rendered into diagnostic PNGs.
    ///
    /// All preview materials remain single-sided. Opposite camera angles are intentional: a wall
    /// with missing/reversed faces should disappear in at least one image rather than being hidden
    /// by a double-sided debug material.
    /// </summary>
    public static class KentridgeCapture
    {
        private const uint Seed = 0x4B454E54u;
        private const int Width = 1600;
        private const int Height = 1000;
        private const float VoxelSize = 0.1f;
        private const int MaterialCount = 18;

        private readonly struct CaptureView
        {
            public readonly string Name;
            public readonly Vector3 HorizontalDirection;
            public readonly bool StreetLevel;

            public CaptureView(string name, Vector3 horizontalDirection, bool streetLevel)
            {
                Name = name;
                HorizontalDirection = horizontalDirection.normalized;
                StreetLevel = streetLevel;
            }
        }

        private static readonly CaptureView[] Views =
        {
            new("overview-ne", new Vector3( 1f, 0f,  1f), false),
            new("overview-nw", new Vector3(-1f, 0f,  1f), false),
            new("overview-se", new Vector3( 1f, 0f, -1f), false),
            new("overview-sw", new Vector3(-1f, 0f, -1f), false),
            new("street-north", new Vector3( 0f, 0f,  1f), true),
            new("street-south", new Vector3( 0f, 0f, -1f), true),
            new("street-east",  new Vector3( 1f, 0f,  0f), true),
            new("street-west",  new Vector3(-1f, 0f,  0f), true),
        };

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "Kentridge");
            Directory.CreateDirectory(outputDirectory);

            FeatureCatalogue catalogue = default;
            RegionTable table = default;
            BrickPool pool = default;
            CpuTransvoxelChunkCache surfaceCache = null;
            GameObject cameraObject = null;
            GameObject terrainObject = null;
            Mesh terrainMesh = null;
            Material terrainMaterial = null;
            Material[] palette = null;
            RenderTexture target = null;
            Texture2D capture = null;
            readonlyCleanup.Clear();

            try
            {
                SettlementPlan plan = KentridgeDefinition.Build(Seed);
                if (plan.Plots.Count != 17)
                    throw new InvalidOperationException($"Expected 17 Kentridge plots, got {plan.Plots.Count}.");
                if (plan.Streets.Count == 0)
                    throw new InvalidOperationException("Kentridge plan contains no streets.");

                TownBounds(plan, out int minX, out int maxX, out int minZ, out int maxZ);

                catalogue = KentridgeCombinedVoxelCatalogue.Build(
                    Seed, BuildSettings(), Allocator.Persistent);
                table = new RegionTable(64, Allocator.Persistent);
                // This diagnostic deliberately keeps the entire authored town resident at once;
                // production streaming does not. Give only the capture enough pool space for that.
                pool = new BrickPool(262144, Allocator.Persistent);

                int featureInstances = 0;
                int featureVoxels = 0;
                int minRegionX = minX >> VoxelDimensions.RegionVoxelEdgeLog2;
                int maxRegionX = maxX >> VoxelDimensions.RegionVoxelEdgeLog2;
                int minRegionZ = minZ >> VoxelDimensions.RegionVoxelEdgeLog2;
                int maxRegionZ = maxZ >> VoxelDimensions.RegionVoxelEdgeLog2;

                for (int rz = minRegionZ; rz <= maxRegionZ; rz++)
                for (int rx = minRegionX; rx <= maxRegionX; rx++)
                {
                    int3 region = new int3(rx, 0, rz);
                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                        in catalogue, Seed, region, ref table, ref pool);
                    if (report.BudgetExceeded)
                        throw new InvalidOperationException($"Kentridge feature budget exceeded in {region}.");
                    featureInstances += report.InstancesRasterised;
                    featureVoxels += report.VoxelsWritten;
                }

                if (featureInstances == 0 || featureVoxels == 0)
                    throw new InvalidOperationException("Kentridge produced no isolated voxel geometry.");

                // This is deliberately a runtime-semantics assertion, not a capture workaround.
                // Kentridge structures must author surface semantics during feature generation so
                // the diagnostic image exercises the same unified reconstruction path as gameplay.
                int surfaceBricks = CountAuthoredSurfaceBricks(ref table, in pool);
                if (surfaceBricks == 0)
                    throw new InvalidOperationException(
                        "Kentridge generated no authored surface-semantics bricks.");

                cameraObject = new GameObject("CI Kentridge Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.68f, 0.82f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                float centreX = (minX + maxX) * 0.05f;
                float centreZ = (minZ + maxZ) * 0.05f;
                int centreVoxelX = (minX + maxX) / 2;
                int centreVoxelZ = (minZ + maxZ) / 2;
                float centreTerrainY = TerrainSampler.HeightAt(
                    centreVoxelX, centreVoxelZ, Seed) * VoxelSize;
                Vector3 overviewFocus = new Vector3(centreX, centreTerrainY + 10f, centreZ);
                float spanMetres = Mathf.Max(maxX - minX, maxZ - minZ) * VoxelSize;
                float overviewDistance = Mathf.Max(120f, spanMetres * 1.32f);

                // First use a high collector camera whose frustum contains the whole town. The
                // unified surface cache exposes visible entries, so this produces one stable set
                // of meshes that every later diagnostic camera renders from a different angle.
                camera.fieldOfView = 55f;
                cameraObject.transform.position = overviewFocus + new Vector3(
                    0f, overviewDistance * 1.15f, -overviewDistance * 0.12f);
                cameraObject.transform.LookAt(overviewFocus);
                camera.farClipPlane = overviewDistance * 4f;

                terrainMesh = BuildTerrainMesh(minX, maxX, minZ, maxZ);
                terrainObject = new GameObject("CI Kentridge Terrain");
                terrainObject.AddComponent<MeshFilter>().sharedMesh = terrainMesh;
                MeshRenderer terrainRenderer = terrainObject.AddComponent<MeshRenderer>();
                Shader previewShader = FindPreviewShader();
                terrainMaterial = NewMaterial(previewShader, "CI Kentridge Terrain",
                    new Color(0.24f, 0.43f, 0.19f, 1f));
                terrainRenderer.sharedMaterial = terrainMaterial;

                palette = BuildPalette(previewShader);
                MaterialPalette materialPalette = BuildMaterialPalette();
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                surfaceCache = new CpuTransvoxelChunkCache();
                // Whole-town CI intentionally exceeds runtime streaming residency/radius.
                surfaceCache.MaxViewDistanceMetres = camera.farClipPlane;
                surfaceCache.MaxResidentChunks = 8192;
                surfaceCache.InvalidateSurfaceBricks(SurfaceChunkSeeds(minX, maxX, minZ, maxZ));
                for (int iteration = 0; iteration < 8192 && surfaceCache.DirtyCount > 0; iteration++)
                {
                    surfaceCache.Prepare(ref table, in pool, in materialPalette,
                        in surfaces, in coatings, null, camera, VoxelSize, 1, 100.0);
                }

                if (surfaceCache.DirtyCount != 0)
                    throw new InvalidOperationException(
                        $"Unified surface extraction did not settle; "
                      + $"{surfaceCache.DirtyCount} chunks remain.");

                IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                    surfaceCache.CollectVisible(camera, VoxelSize, 1);
                if (visible.Count == 0)
                    throw new InvalidOperationException(
                        "Kentridge unified surface extraction produced no visible chunks.");

                int hardTriangles = 0;
                for (int i = 0; i < visible.Count; i++)
                {
                    Mesh mesh = MeshEntry(visible[i], out int triangles);
                    hardTriangles += triangles;
                    var root = new GameObject($"CI Kentridge Chunk {visible[i].Coordinate}");
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    root.AddComponent<MeshRenderer>().sharedMaterials = palette;
                    readonlyCleanup.Add(root, mesh);
                }

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Kentridge Diagnostic Capture",
                    antiAliasing = 4,
                };
                target.Create();
                capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                camera.targetTexture = target;

                var viewMetadata = new List<string>(Views.Length);
                for (int i = 0; i < Views.Length; i++)
                {
                    CaptureView view = Views[i];
                    ConfigureCamera(camera, cameraObject.transform, view,
                                    overviewFocus, spanMetres, overviewDistance,
                                    centreVoxelX, centreVoxelZ, centreTerrainY);
                    Capture(camera, target, capture,
                            Path.Combine(outputDirectory, "kentridge-" + view.Name + ".png"));
                    viewMetadata.Add($"view={view.Name} camera={camera.transform.position:F2} " +
                                     $"rotation={camera.transform.eulerAngles:F2} fov={camera.fieldOfView:F1}");
                }

                string metadata =
                    $"seed={Seed}\n" +
                    $"plots={plan.Plots.Count}\n" +
                    $"streets={plan.Streets.Count}\n" +
                    $"featureInstances={featureInstances}\n" +
                    $"featureVoxels={featureVoxels}\n" +
                    $"surfaceBricks={surfaceBricks}\n" +
                    $"surfaceChunks={visible.Count}\n" +
                    $"surfaceTriangles={hardTriangles}\n" +
                    $"boundsDm={minX},{minZ}..{maxX},{maxZ}\n" +
                    $"captures={Views.Length}\n" +
                    string.Join("\n", viewMetadata) + "\n";
                File.WriteAllText(Path.Combine(outputDirectory, "kentridge-overview.txt"), metadata);
                Debug.Log($"CI Kentridge captures written to {outputDirectory}\n{metadata}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }
            finally
            {
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                readonlyCleanup.Dispose();
                if (terrainObject != null) UnityEngine.Object.DestroyImmediate(terrainObject);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (terrainMesh != null) UnityEngine.Object.DestroyImmediate(terrainMesh);
                if (terrainMaterial != null) UnityEngine.Object.DestroyImmediate(terrainMaterial);
                if (palette != null)
                    for (int i = 0; i < palette.Length; i++)
                        if (palette[i] != null) UnityEngine.Object.DestroyImmediate(palette[i]);
                surfaceCache?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        private static void ConfigureCamera(Camera camera, Transform transform, CaptureView view,
                                            Vector3 overviewFocus, float spanMetres,
                                            float overviewDistance, int centreVoxelX,
                                            int centreVoxelZ, float centreTerrainY)
        {
            if (!view.StreetLevel)
            {
                camera.fieldOfView = 39f;
                Vector3 position = overviewFocus
                    + view.HorizontalDirection * overviewDistance
                    + Vector3.up * (overviewDistance * 0.62f);
                transform.position = position;
                transform.LookAt(overviewFocus);
                camera.farClipPlane = overviewDistance * 3.5f;
                return;
            }

            float horizontalDistance = Mathf.Max(52f, spanMetres * 0.43f);
            Vector3 horizontalOffset = view.HorizontalDirection * horizontalDistance;
            int cameraVoxelX = centreVoxelX + Mathf.RoundToInt(horizontalOffset.x / VoxelSize);
            int cameraVoxelZ = centreVoxelZ + Mathf.RoundToInt(horizontalOffset.z / VoxelSize);
            float cameraTerrainY = TerrainSampler.HeightAt(cameraVoxelX, cameraVoxelZ, Seed) * VoxelSize;

            camera.fieldOfView = 52f;
            transform.position = new Vector3(
                overviewFocus.x + horizontalOffset.x,
                cameraTerrainY + 3.4f,
                overviewFocus.z + horizontalOffset.z);
            Vector3 streetFocus = new Vector3(overviewFocus.x, centreTerrainY + 5.2f, overviewFocus.z);
            transform.LookAt(streetFocus);
            camera.farClipPlane = Mathf.Max(240f, spanMetres * 2.2f);
        }

        private static void Capture(Camera camera, RenderTexture target, Texture2D capture,
                                    string outputPath)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                capture.Apply(false, false);
                File.WriteAllBytes(outputPath, capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static readonly CaptureCleanup readonlyCleanup = new CaptureCleanup();

        private sealed class CaptureCleanup
        {
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly List<Mesh> _meshes = new List<Mesh>();
            public void Add(GameObject root, Mesh mesh) { _objects.Add(root); _meshes.Add(mesh); }
            public void Clear() { _objects.Clear(); _meshes.Clear(); }
            public void Dispose()
            {
                for (int i = 0; i < _objects.Count; i++)
                    if (_objects[i] != null) UnityEngine.Object.DestroyImmediate(_objects[i]);
                for (int i = 0; i < _meshes.Count; i++)
                    if (_meshes[i] != null) UnityEngine.Object.DestroyImmediate(_meshes[i]);
                Clear();
            }
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

        private static int CountAuthoredSurfaceBricks(ref RegionTable table, in BrickPool pool)
        {
            int count = 0;
            NativeArray<int3> resident = table.GetResidentCoords(Allocator.Temp);
            try
            {
                for (int r = 0; r < resident.Length; r++)
                {
                    if (!table.TryGetRegion(resident[r], out Region region)) continue;
                    for (int i = 0; i < region.BrickRefs.Length; i++)
                    {
                        BrickRef brick = region.BrickRefs[i];
                        if (!brick.IsMixed) continue;
                        int offset = brick.PoolIndex * VoxelDimensions.VoxelsPerBrick;
                        for (int voxel = 0; voxel < VoxelDimensions.VoxelsPerBrick; voxel++)
                        {
                            if (pool.SurfaceSemantics[offset + voxel] == 0
                                && pool.BoundarySamples[offset + voxel] == 0)
                                continue;
                            count++;
                            break;
                        }
                    }
                }
            }
            finally
            {
                resident.Dispose();
            }
            return count;
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

            const int padding = 96;
            minX -= padding; maxX += padding; minZ -= padding; maxZ += padding;
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
                float wy = TerrainSampler.HeightAt(wx, wz, Seed) * VoxelSize - 0.08f;
                vertices[x + z * countX] = new Vector3(wx * VoxelSize, wy, wz * VoxelSize);
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

            var mesh = new Mesh { name = "CI Kentridge Terrain", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static MaterialPalette BuildMaterialPalette()
        {
            MaterialPalette palette = default;
            for (byte material = 1; material < MaterialCount; material++)
                palette.Register(material, 128, DestructionClass.Crumble,
                                 SurfaceStyles.Planar, uint.MaxValue);
            return palette;
        }

        private static List<int3> SurfaceChunkSeeds(int minX, int maxX, int minZ, int maxZ)
        {
            int edge = CpuTransvoxelChunkCache.VoxelsPerAxis;
            int minChunkX = FloorDiv(minX, edge) - 1;
            int maxChunkX = FloorDiv(maxX, edge) + 1;
            int minChunkZ = FloorDiv(minZ, edge) - 1;
            int maxChunkZ = FloorDiv(maxZ, edge) + 1;
            int maxChunkY = FloorDiv(TerrainSampler.MaxHeight, edge);
            var result = new List<int3>();
            for (int cy = 0; cy <= maxChunkY; cy++)
            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
                result.Add(new int3(cx * CpuTransvoxelChunkCache.BricksPerAxis,
                                    cy * CpuTransvoxelChunkCache.BricksPerAxis,
                                    cz * CpuTransvoxelChunkCache.BricksPerAxis));
            return result;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static Mesh MeshEntry(CpuTransvoxelChunkCache.Entry entry,
                                      out int triangleCount)
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

            var perMaterial = new List<int>[MaterialCount];
            for (int i = 0; i < MaterialCount; i++) perMaterial[i] = new List<int>();
            for (int i = 0; i + 2 < sourceIndices.Length; i += 3)
            {
                int first = (int)sourceIndices[i];
                int material = (int)(sourceVertices[first].Material & 0xFFu);
                if ((uint)material >= MaterialCount) material = 1;
                perMaterial[material].Add((int)sourceIndices[i]);
                perMaterial[material].Add((int)sourceIndices[i + 1]);
                perMaterial[material].Add((int)sourceIndices[i + 2]);
            }

            var mesh = new Mesh
            {
                name = $"CI Kentridge {entry.Coordinate}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.subMeshCount = MaterialCount;
            for (int material = 0; material < MaterialCount; material++)
                mesh.SetTriangles(perMaterial[material], material, false);
            mesh.RecalculateBounds();
            triangleCount = sourceIndices.Length / 3;
            return mesh;
        }

        private static Shader FindPreviewShader()
        {
            // Unlit keeps material distinctions readable in headless CI while retaining normal
            // backface culling. Do not use a double-sided shader here: disappearing reverse faces
            // are one of the artifacts this visual test is supposed to expose.
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No CI preview shader was found.");
            return shader;
        }

        private static Material[] BuildPalette(Shader shader)
        {
            var colours = new Color[MaterialCount];
            for (int i = 0; i < colours.Length; i++)
                colours[i] = new Color(0.55f, 0.55f, 0.55f, 1f);
            colours[1] = new Color(0.68f, 0.64f, 0.55f, 1f); // stone
            colours[2] = new Color(0.30f, 0.15f, 0.06f, 1f); // timber
            colours[3] = new Color(0.70f, 0.62f, 0.44f, 1f);
            colours[4] = new Color(0.30f, 0.70f, 0.86f, 1f); // glass
            colours[6] = new Color(0.24f, 0.25f, 0.28f, 1f); // dark masonry
            colours[7] = new Color(0.20f, 0.26f, 0.36f, 1f); // slate
            colours[8] = new Color(0.66f, 0.20f, 0.10f, 1f); // roof tile
            colours[9] = new Color(0.52f, 0.16f, 0.61f, 1f); // cloth
            colours[10] = new Color(0.24f, 0.54f, 0.18f, 1f);
            colours[13] = new Color(0.38f, 0.25f, 0.13f, 1f); // road
            colours[14] = new Color(0.16f, 0.42f, 0.13f, 1f); // moss
            colours[15] = new Color(1.00f, 0.63f, 0.12f, 1f); // warm window

            var result = new Material[MaterialCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = NewMaterial(shader, $"CI Kentridge Material {i}", colours[i]);
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
    }
}
