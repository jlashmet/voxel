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
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.CI
{
    internal static partial class KentridgeUnifiedCaptureV2
    {
        private const uint Seed = 0x4B454E54u;
        private const float VoxelSize = 0.1f;
        private const int MaterialCount = 18;
        private const int PresentationCount = MaterialCount * 2;
        private const int Width = 1600;
        private const int Height = 1000;

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
            new("overview-ne", new Vector3( 1,0, 1), false),
            new("overview-nw", new Vector3(-1,0, 1), false),
            new("overview-se", new Vector3( 1,0,-1), false),
            new("overview-sw", new Vector3(-1,0,-1), false),
            new("street-north", new Vector3(0,0, 1), true),
            new("street-south", new Vector3(0,0,-1), true),
            new("street-east",  new Vector3( 1,0,0), true),
            new("street-west",  new Vector3(-1,0,0), true),
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
            Material[] palette = null;
            RenderTexture target = null;
            Texture2D image = null;
            var objects = new List<GameObject>();
            var meshes = new List<Mesh>();

            try
            {
                SettlementPlan plan = KentridgeDefinition.Build(Seed);
                if (plan.Plots.Count != 17 || plan.Streets.Count != 4)
                    throw new InvalidOperationException("Kentridge stable settlement contract changed.");

                TownBounds(plan, out int minX, out int maxX, out int minZ, out int maxZ);
                table = new RegionTable(96, Allocator.Persistent);
                pool = new BrickPool(262144, Allocator.Persistent);
                LoadTerrain(minX, maxX, minZ, maxZ, ref table);
                catalogue = KentridgeCombinedVoxelCatalogue.Build(Seed, BuildSettings(), Allocator.Persistent);

                var featureReads = new RegionReadSource(in table, in pool);
                var featureMutations = new RegionMutationStore(in table, in pool);
                int instances = 0;
                int voxels = 0;
                int minRX = minX >> VoxelDimensions.RegionVoxelEdgeLog2;
                int maxRX = maxX >> VoxelDimensions.RegionVoxelEdgeLog2;
                int minRZ = minZ >> VoxelDimensions.RegionVoxelEdgeLog2;
                int maxRZ = maxZ >> VoxelDimensions.RegionVoxelEdgeLog2;
                for (int rz = minRZ; rz <= maxRZ; rz++)
                for (int rx = minRX; rx <= maxRX; rx++)
                {
                    featureReads.Refresh(in table, in pool);
                    featureMutations.Refresh(in table, in pool);
                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                        in catalogue, Seed, new int3(rx, 0, rz), featureReads, featureMutations);
                    if (report.BudgetExceeded)
                        throw new InvalidOperationException($"Kentridge generation limit exceeded in {rx},{rz}.");
                    instances += report.InstancesRasterised;
                    voxels += report.VoxelsWritten;
                }

                cameraObject = new GameObject("CI Kentridge Unified V2 Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.68f, 0.82f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                int centreX = (minX + maxX) / 2;
                int centreZ = (minZ + maxZ) / 2;
                float centreY = SurfaceY(centreX, centreZ);
                Vector3 focus = new(centreX * VoxelSize, centreY + 10f, centreZ * VoxelSize);
                float span = Mathf.Max(maxX - minX, maxZ - minZ) * VoxelSize;
                float distance = Mathf.Max(120f, span * 1.32f);
                camera.fieldOfView = 55f;
                camera.transform.position = focus + new Vector3(0f, distance * 1.15f, -distance * 0.12f);
                camera.transform.LookAt(focus);
                camera.farClipPlane = distance * 4f;

                MaterialPalette materialPalette = BuildMaterialPalette();
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                VoxelEngine.Storage.Api.MaterialPaletteView materialPaletteView = materialPalette;
                VoxelEngine.Storage.Api.SurfaceCatalogueView surfaceView = surfaces;
                VoxelEngine.Storage.Api.CoatingCatalogueView coatingView = coatings;
                cache = new CpuTransvoxelChunkCache
                {
                    MaxResidentChunks = 32768,
                    MaxViewDistanceMetres = 10000f,
                };
                cache.InvalidateSurfaceBricks(ChunkSeeds(minX, maxX, minZ, maxZ));

                var readSource = new RegionReadSource(in table, in pool);
                int previousDirty = int.MaxValue;
                int stalled = 0;
                for (int iteration = 0; iteration < 65536 && cache.DirtyCount > 0; iteration++)
                {
                    cache.Prepare(readSource, in materialPaletteView,
                        in surfaceView, in coatingView, null, camera, VoxelSize, 1, 100.0);
                    int dirty = cache.DirtyCount;
                    if (dirty == previousDirty)
                    {
                        stalled++;
                        if ((stalled & 7) == 0) Thread.Sleep(1);
                    }
                    else
                    {
                        previousDirty = dirty;
                        stalled = 0;
                    }
                }

                // Some synthetic border seeds intentionally sit outside the resident terrain region
                // set and can never become buildable. Render the completed resident set, but record
                // the residual so CI still exposes unexpected growth in unavailable border chunks.
                int pendingChunks = cache.DirtyCount;

                IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible = cache.CollectVisible(camera, VoxelSize, 1);
                if (visible.Count == 0) throw new InvalidOperationException("Kentridge produced no visible chunks.");

                palette = BuildPalette(FindPreviewShader());
                int triangles = 0;
                int architecturalTriangles = 0;
                for (int i = 0; i < visible.Count; i++)
                {
                    Mesh mesh = BuildMesh(visible[i], out int total, out int architectural);
                    triangles += total;
                    architecturalTriangles += architectural;
                    var rootObject = new GameObject($"CI Kentridge Unified V2 {visible[i].Coordinate}");
                    rootObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    rootObject.AddComponent<MeshRenderer>().sharedMaterials = palette;
                    objects.Add(rootObject);
                    meshes.Add(mesh);
                }
                if (architecturalTriangles == 0) throw new InvalidOperationException("Kentridge produced no architectural triangles.");

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    name = "CI Kentridge Unified V2 Capture",
                };
                target.Create();
                image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                camera.targetTexture = target;

                var views = new List<string>();
                for (int i = 0; i < Views.Length; i++)
                {
                    ConfigureCamera(camera, Views[i], focus, span, distance, centreX, centreZ, centreY);
                    Capture(camera, target, image, Path.Combine(output, "kentridge-" + Views[i].Name + ".png"));
                    views.Add($"view={Views[i].Name} camera={camera.transform.position:F2} rotation={camera.transform.eulerAngles:F2} fov={camera.fieldOfView:F1}");
                }

                File.WriteAllText(Path.Combine(output, "kentridge-overview.txt"),
                    $"capture=unified-v2\nseed={Seed}\nplots={plan.Plots.Count}\nstreets={plan.Streets.Count}\n" +
                    $"featureInstances={instances}\nfeatureVoxels={voxels}\n" +
                    $"surfaceChunks={visible.Count}\nsurfaceTriangles={triangles}\n" +
                    $"architecturalTriangles={architecturalTriangles}\nknownChunks={cache.KnownCount}\nresidentChunks={cache.ResidentCount}\n" +
                    $"pendingChunks={pendingChunks}\n" +
                    string.Join("\n", views) + "\n");
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
                for (int i = 0; i < objects.Count; i++) if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                for (int i = 0; i < meshes.Count; i++) if (meshes[i] != null) UnityEngine.Object.DestroyImmediate(meshes[i]);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyPalette(palette);
                cache?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }
    }
}
