using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Terrain.Runtime;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Runtime-faithful isolated visual test for Kentridge.
    ///
    /// Unlike the original capture, this seeds real voxel terrain first, rasterises the combined
    /// Kentridge catalogue into that storage, and then renders the production smooth Transvoxel and
    /// hard architectural layers from the same RegionTable. Roads, plot cut/fill, foundations, and
    /// structure/terrain ownership are therefore visible in the artifact instead of being hidden
    /// behind a separate untouched heightfield mesh.
    /// </summary>
    public static class KentridgeRuntimeCapture
    {
        private const uint Seed = 0x4B454E54u;
        private const int Width = 1600;
        private const int Height = 1000;
        private const float VoxelSize = 0.1f;
        private const int MaterialCount = 18;

        private readonly struct CaptureView
        {
            public readonly string Name;
            public readonly Vector3 Direction;
            public readonly bool StreetLevel;

            public CaptureView(string name, Vector3 direction, bool streetLevel)
            {
                Name = name;
                Direction = direction.normalized;
                StreetLevel = streetLevel;
            }
        }

        private static readonly CaptureView[] Views =
        {
            new CaptureView("overview-ne", new Vector3( 1f, 0f,  1f), false),
            new CaptureView("overview-nw", new Vector3(-1f, 0f,  1f), false),
            new CaptureView("overview-se", new Vector3( 1f, 0f, -1f), false),
            new CaptureView("overview-sw", new Vector3(-1f, 0f, -1f), false),
            new CaptureView("street-north", new Vector3( 0f, 0f,  1f), true),
            new CaptureView("street-south", new Vector3( 0f, 0f, -1f), true),
            new CaptureView("street-east",  new Vector3( 1f, 0f,  0f), true),
            new CaptureView("street-west",  new Vector3(-1f, 0f,  0f), true),
        };

        public static void Run()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "Kentridge");
            Directory.CreateDirectory(outputDirectory);

            FeatureCatalogue catalogue = default;
            RegionTable table = default;
            BrickPool pool = default;
            CpuTransvoxelChunkCache smoothCache = null;
            GameObject cameraObject = null;
            Material[] hardPalette = null;
            Material[] smoothPalette = null;
            RenderTexture target = null;
            Texture2D capture = null;
            s_Cleanup.Clear();

            try
            {
                SettlementPlan plan = KentridgeDefinition.Build(Seed);
                if (plan.Plots.Count != 17)
                    throw new InvalidOperationException(
                        $"Expected 17 Kentridge plots, got {plan.Plots.Count}.");
                if (plan.Streets.Count == 0)
                    throw new InvalidOperationException("Kentridge plan contains no streets.");

                TownBounds(plan, out int minX, out int maxX, out int minZ, out int maxZ);

                table = new RegionTable(96, Allocator.Persistent);
                pool = new BrickPool(65536, Allocator.Persistent);
                LoadTerrain(minX, maxX, minZ, maxZ, ref table);

                catalogue = KentridgeCombinedVoxelCatalogue.Build(
                    Seed, BuildSettings(), Allocator.Persistent);

                var featureReads = new RegionReadSource(in table, in pool);
                var featureMutations = new RegionMutationStore(in table, in pool);
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
                    featureReads.Refresh(in table, in pool);
                    featureMutations.Refresh(in table, in pool);
                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                        in catalogue, Seed, region, featureReads, featureMutations);
                    if (report.BudgetExceeded)
                        throw new InvalidOperationException(
                            $"Kentridge feature budget exceeded in {region}.");
                    featureInstances += report.InstancesRasterised;
                    featureVoxels += report.VoxelsWritten;
                }

                if (featureInstances == 0 || featureVoxels == 0)
                    throw new InvalidOperationException(
                        "Kentridge produced no voxel geometry on generated terrain.");

                int surfaceBricks = CountAuthoredSurfaceBricks(ref table, in pool);
                if (surfaceBricks == 0)
                    throw new InvalidOperationException(
                        "Kentridge generated no authored surface-semantics bricks.");

                cameraObject = new GameObject("CI Kentridge Runtime Camera");
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

                camera.fieldOfView = 55f;
                cameraObject.transform.position = overviewFocus + new Vector3(
                    0f, overviewDistance * 1.15f, -overviewDistance * 0.12f);
                cameraObject.transform.LookAt(overviewFocus);
                camera.farClipPlane = overviewDistance * 4f;

                Shader previewShader = FindPreviewShader();
                hardPalette = BuildHardPalette(previewShader);
                smoothPalette = BuildSmoothPalette(previewShader);

                smoothCache = new CpuTransvoxelChunkCache();
                smoothCache.MaxViewDistanceMetres = camera.farClipPlane;
                smoothCache.MaxResidentChunks = 8192;
                List<int3> smoothSeeds = SmoothChunkSeeds(minX, maxX, minZ, maxZ);
                smoothCache.InvalidateSurfaceBricks(smoothSeeds);
                MaterialPalette materialPalette = BuildMaterialPalette();
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                VoxelEngine.Storage.Api.MaterialPaletteView materialPaletteView = materialPalette;
                VoxelEngine.Storage.Api.SurfaceCatalogueView surfaceView = surfaces;
                VoxelEngine.Storage.Api.CoatingCatalogueView coatingView = coatings;

                var readSource = new RegionReadSource(in table, in pool);
                int previousDirty = int.MaxValue;
                int stalled = 0;
                for (int iteration = 0; iteration < 65536 && smoothCache.DirtyCount > 0; iteration++)
                {
                    smoothCache.Prepare(readSource, in materialPaletteView,
                        in surfaceView, in coatingView, null, camera, VoxelSize,
                        frame: 1, budgetMs: 100.0);
                    int dirty = smoothCache.DirtyCount;
                    if (dirty == previousDirty)
                    {
                        stalled++;
                        if ((stalled & 7) == 0) Thread.Sleep(1);
                    }
                    else
                    {
                        stalled = 0;
                        previousDirty = dirty;
                    }
                }

                if (smoothCache.DirtyCount != 0)
                    throw new InvalidOperationException(
                        $"Smooth extraction did not settle; {smoothCache.DirtyCount} chunks remain.");

                IReadOnlyList<CpuTransvoxelChunkCache.Entry> smoothVisible =
                    smoothCache.CollectVisible(camera, VoxelSize, frame: 1);
                if (smoothVisible.Count == 0)
                    throw new InvalidOperationException(
                        "Kentridge smooth terrain extraction produced no visible chunks.");

                int smoothTriangles = 0;
                for (int i = 0; i < smoothVisible.Count; i++)
                {
                    Mesh mesh = MeshEntry(smoothVisible[i], out int triangles);
                    smoothTriangles += triangles;
                    AddMeshObject(
                        $"CI Kentridge Smooth {smoothVisible[i].Coordinate}", mesh, smoothPalette);
                }

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Kentridge Runtime Diagnostic Capture",
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
                    viewMetadata.Add(
                        $"view={view.Name} camera={camera.transform.position:F2} " +
                        $"rotation={camera.transform.eulerAngles:F2} fov={camera.fieldOfView:F1}");
                }

                string metadata =
                    $"capture=runtime-voxel-terrain\n" +
                    $"seed={Seed}\n" +
                    $"plots={plan.Plots.Count}\n" +
                    $"streets={plan.Streets.Count}\n" +
                    $"featureInstances={featureInstances}\n" +
                    $"featureVoxels={featureVoxels}\n" +
                    $"surfaceBricks={surfaceBricks}\n" +
                    $"smoothChunks={smoothVisible.Count}\n" +
                    $"smoothTriangles={smoothTriangles}\n" +
                    $"boundsDm={minX},{minZ}..{maxX},{maxZ}\n" +
                    $"captures={Views.Length}\n" +
                    string.Join("\n", viewMetadata) + "\n";
                File.WriteAllText(
                    Path.Combine(outputDirectory, "kentridge-overview.txt"), metadata);
                Debug.Log($"CI Kentridge runtime captures written to {outputDirectory}\n{metadata}");
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
                s_Cleanup.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyPalette(hardPalette);
                DestroyPalette(smoothPalette);
                smoothCache?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        private static void LoadTerrain(int minX, int maxX, int minZ, int maxZ,
                                        ref RegionTable table)
        {
            int minRegionX = (minX >> VoxelDimensions.RegionVoxelEdgeLog2) - 1;
            int maxRegionX = (maxX >> VoxelDimensions.RegionVoxelEdgeLog2) + 1;
            int minRegionZ = (minZ >> VoxelDimensions.RegionVoxelEdgeLog2) - 1;
            int maxRegionZ = (maxZ >> VoxelDimensions.RegionVoxelEdgeLog2) + 1;
            var generation = new RegionGenerationStore(in table);

            for (int rz = minRegionZ; rz <= maxRegionZ; rz++)
            for (int rx = minRegionX; rx <= maxRegionX; rx++)
            {
                int3 regionCoord = new int3(rx, 0, rz);
                TerrainGenerator.Generate(generation, regionCoord, Seed, CaptureTerrainMaterials.Default);
            }
        }

        private static List<int3> SmoothChunkSeeds(int minX, int maxX, int minZ, int maxZ)
        {
            int edge = CpuTransvoxelChunkCache.BaseVoxelsPerAxis;
            int minChunkX = FloorDiv(minX, edge) - 1;
            int maxChunkX = FloorDiv(maxX, edge) + 1;
            int minChunkZ = FloorDiv(minZ, edge) - 1;
            int maxChunkZ = FloorDiv(maxZ, edge) + 1;
            int maxChunkY = FloorDiv(TerrainSampler.MaxHeight, edge);
            var result = new List<int3>(
                (maxChunkX - minChunkX + 1) *
                (maxChunkZ - minChunkZ + 1) *
                (maxChunkY + 1));

            for (int cy = 0; cy <= maxChunkY; cy++)
            for (int cz = minChunkZ; cz <= maxChunkZ; cz++)
            for (int cx = minChunkX; cx <= maxChunkX; cx++)
            {
                result.Add(new int3(
                    cx * CpuTransvoxelChunkCache.BaseBricksPerAxis,
                    cy * CpuTransvoxelChunkCache.BaseBricksPerAxis,
                    cz * CpuTransvoxelChunkCache.BaseBricksPerAxis));
            }
            return result;
        }

        private static MaterialPalette BuildMaterialPalette()
        {
            MaterialPalette palette = default;
            for (byte material = 1; material < MaterialCount; material++)
                palette.Register(material, 128, DestructionClass.Crumble,
                                 SurfaceStyles.Planar, uint.MaxValue);
            return palette;
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static void ConfigureCamera(Camera camera, Transform transform, CaptureView view,
                                            Vector3 overviewFocus, float spanMetres,
                                            float overviewDistance, int centreVoxelX,
                                            int centreVoxelZ, float centreTerrainY)
        {
            if (!view.StreetLevel)
            {
                camera.fieldOfView = 39f;
                transform.position = overviewFocus
                    + view.Direction * overviewDistance
                    + Vector3.up * (overviewDistance * 0.62f);
                transform.LookAt(overviewFocus);
                camera.farClipPlane = overviewDistance * 3.5f;
                return;
            }

            float horizontalDistance = Mathf.Max(52f, spanMetres * 0.43f);
            Vector3 horizontalOffset = view.Direction * horizontalDistance;
            int cameraVoxelX = centreVoxelX + Mathf.RoundToInt(horizontalOffset.x / VoxelSize);
            int cameraVoxelZ = centreVoxelZ + Mathf.RoundToInt(horizontalOffset.z / VoxelSize);
            float cameraTerrainY = TerrainSampler.HeightAt(
                cameraVoxelX, cameraVoxelZ, Seed) * VoxelSize;

            camera.fieldOfView = 52f;
            transform.position = new Vector3(
                overviewFocus.x + horizontalOffset.x,
                cameraTerrainY + 3.4f,
                overviewFocus.z + horizontalOffset.z);
            transform.LookAt(new Vector3(
                overviewFocus.x, centreTerrainY + 5.2f, overviewFocus.z));
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

        private static void AddMeshObject(string name, Mesh mesh, Material[] palette)
        {
            var root = new GameObject(name);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterials = palette;
            s_Cleanup.Add(root, mesh);
        }

        private static readonly CaptureCleanup s_Cleanup = new CaptureCleanup();

        private sealed class CaptureCleanup
        {
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly List<Mesh> _meshes = new List<Mesh>();

            public void Add(GameObject root, Mesh mesh)
            {
                _objects.Add(root);
                _meshes.Add(mesh);
            }

            public void Clear()
            {
                _objects.Clear();
                _meshes.Clear();
            }

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
            minX -= padding;
            maxX += padding;
            minZ -= padding;
            maxZ += padding;
        }

        private static Mesh MeshEntry(CpuTransvoxelChunkCache.Entry entry,
                                      out int triangleCount)
        {
            return MeshFromBuffers(
                entry.Vertices, entry.Indices, entry.IndexCount,
                $"CI Kentridge Smooth {entry.Coordinate}", out triangleCount);
        }

        private static Mesh MeshFromBuffers(ComputeBuffer vertexBuffer, GraphicsBuffer indexBuffer,
                                            int indexCount, string name,
                                            out int triangleCount)
        {
            var sourceVertices = new SmoothSurfaceVertex[vertexBuffer.count];
            var sourceIndices = new uint[indexCount];
            vertexBuffer.GetData(sourceVertices);
            indexBuffer.GetData(sourceIndices, 0, 0, indexCount);

            var vertices = new Vector3[sourceVertices.Length];
            var normals = new Vector3[sourceVertices.Length];
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                vertices[i] = sourceVertices[i].Position;
                normals[i] = sourceVertices[i].Normal;
            }

            var perMaterial = new List<int>[MaterialCount];
            for (int i = 0; i < MaterialCount; i++)
                perMaterial[i] = new List<int>();

            for (int i = 0; i + 2 < sourceIndices.Length; i += 3)
            {
                int first = (int)sourceIndices[i];
                int material = (int)sourceVertices[first].Material;
                if ((uint)material >= MaterialCount) material = 1;
                perMaterial[material].Add((int)sourceIndices[i]);
                perMaterial[material].Add((int)sourceIndices[i + 1]);
                perMaterial[material].Add((int)sourceIndices[i + 2]);
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
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
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No CI preview shader was found.");
            return shader;
        }

        private static Material[] BuildHardPalette(Shader shader)
        {
            Color[] colours = BaseColours();
            colours[1] = new Color(0.68f, 0.64f, 0.55f, 1f); // masonry
            colours[2] = new Color(0.30f, 0.15f, 0.06f, 1f); // timber
            colours[4] = new Color(0.30f, 0.70f, 0.86f, 1f); // glass
            colours[6] = new Color(0.24f, 0.25f, 0.28f, 1f); // dark masonry
            colours[7] = new Color(0.20f, 0.26f, 0.36f, 1f); // slate
            colours[8] = new Color(0.66f, 0.20f, 0.10f, 1f); // roof tile
            colours[9] = new Color(0.52f, 0.16f, 0.61f, 1f); // cloth
            colours[15] = new Color(1.00f, 0.63f, 0.12f, 1f); // warm window
            return BuildPalette(shader, "Hard", colours);
        }

        private static Material[] BuildSmoothPalette(Shader shader)
        {
            Color[] colours = BaseColours();
            // Diagnostic colours intentionally distinguish smooth terrain from masonry even though
            // both may currently use material id 1 in storage.
            colours[1] = new Color(0.25f, 0.48f, 0.20f, 1f); // terrain/stone field
            colours[3] = new Color(0.70f, 0.62f, 0.44f, 1f); // sand
            colours[5] = new Color(0.23f, 0.20f, 0.18f, 1f); // bedrock
            colours[10] = new Color(0.24f, 0.54f, 0.18f, 1f); // grass
            colours[13] = new Color(0.38f, 0.25f, 0.13f, 1f); // roads / prepared plots
            colours[14] = new Color(0.16f, 0.42f, 0.13f, 1f); // moss
            return BuildPalette(shader, "Smooth", colours);
        }

        private static Color[] BaseColours()
        {
            var colours = new Color[MaterialCount];
            for (int i = 0; i < colours.Length; i++)
                colours[i] = new Color(0.55f, 0.55f, 0.55f, 1f);
            return colours;
        }

        private static Material[] BuildPalette(Shader shader, string label, Color[] colours)
        {
            var result = new Material[MaterialCount];
            for (int i = 0; i < result.Length; i++)
            {
                var material = new Material(shader)
                {
                    name = $"CI Kentridge {label} Material {i}"
                };
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", colours[i]);
                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", colours[i]);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", 0f);
                result[i] = material;
            }
            return result;
        }

        private static void DestroyPalette(Material[] palette)
        {
            if (palette == null) return;
            for (int i = 0; i < palette.Length; i++)
                if (palette[i] != null) UnityEngine.Object.DestroyImmediate(palette[i]);
        }
    }
}
