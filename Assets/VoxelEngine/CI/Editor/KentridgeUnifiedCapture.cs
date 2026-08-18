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
    /// Whole-town Kentridge diagnostic that uses the production unified surface extractor over a
    /// terrain-populated voxel world. Natural/landform surfaces use smooth reconstruction while
    /// Structure and Infrastructure semantics remain faceted. This is intentionally CI-only: the
    /// enlarged pool/cache limits let one capture keep the entire settlement resident at once.
    /// </summary>
    internal static class KentridgeUnifiedCapture
    {
        private const uint Seed = 0x4B454E54u;
        private const float VoxelSize = 0.1f;
        private const int Width = 1600;
        private const int Height = 1000;
        private const int MaterialCount = 18;
        private const int PresentationCount = MaterialCount * 2;

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
                    featureReads.Refresh(in table, in pool);
                    featureMutations.Refresh(in table, in pool);
                    FeatureGenerationReport report = FeatureGeneration.GenerateRegion(
                        in catalogue, Seed, new int3(rx, 0, rz), featureReads, featureMutations);
                    if (report.BudgetExceeded)
                        throw new InvalidOperationException(
                            $"Kentridge feature budget exceeded in region {rx},{rz}.");
                    featureInstances += report.InstancesRasterised;
                    featureVoxels += report.VoxelsWritten;
                }

                if (featureInstances == 0 || featureVoxels == 0)
                    throw new InvalidOperationException("Kentridge generated no authored geometry.");

                cameraObject = new GameObject("CI Kentridge Unified Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.68f, 0.82f, 1f);
                camera.nearClipPlane = 0.1f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                int centreDmX = (minX + maxX) / 2;
                int centreDmZ = (minZ + maxZ) / 2;
                float centreSurfaceY = AuthoredSurfaceYMetres(centreDmX, centreDmZ);
                Vector3 focus = new Vector3(
                    centreDmX * VoxelSize,
                    centreSurfaceY + 10f,
                    centreDmZ * VoxelSize);
                float spanMetres = Mathf.Max(maxX - minX, maxZ - minZ) * VoxelSize;
                float overviewDistance = Mathf.Max(120f, spanMetres * 1.32f);

                camera.fieldOfView = 55f;
                camera.transform.position = focus + new Vector3(
                    0f, overviewDistance * 1.15f, -overviewDistance * 0.12f);
                camera.transform.LookAt(focus);
                camera.farClipPlane = overviewDistance * 4f;

                MaterialPalette materials = BuildMaterialPalette();
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                VoxelEngine.Storage.Api.MaterialPaletteView materialPaletteView = materials;
                VoxelEngine.Storage.Api.SurfaceCatalogueView surfaceView = surfaces;
                VoxelEngine.Storage.Api.CoatingCatalogueView coatingView = coatings;
                cache = new CpuTransvoxelChunkCache
                {
                    MaxResidentChunks = 16384,
                    MaxViewDistanceMetres = 10000f,
                };
                cache.InvalidateSurfaceBricks(SurfaceChunkSeeds(minX, maxX, minZ, maxZ));

                var readSource = new RegionReadSource(in table, in pool);
                int previousDirty = int.MaxValue;
                int stalled = 0;
                for (int iteration = 0; iteration < 65536 && cache.DirtyCount > 0; iteration++)
                {
                    cache.Prepare(readSource, in materialPaletteView,
                        in surfaceView, in coatingView, null, camera, VoxelSize,
                        frame: 1, budgetMs: 100.0);

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

                if (cache.DirtyCount != 0)
                    throw new InvalidOperationException(
                        $"Kentridge unified extraction did not settle; {cache.DirtyCount} chunks remain.");

                IReadOnlyList<CpuTransvoxelChunkCache.Entry> visible =
                    cache.CollectVisible(camera, VoxelSize, frame: 1);
                if (visible.Count == 0)
                    throw new InvalidOperationException("Kentridge unified extraction produced no visible chunks.");

                palette = BuildPresentationPalette(FindPreviewShader());
                int triangles = 0;
                int architecturalTriangles = 0;
                for (int i = 0; i < visible.Count; i++)
                {
                    Mesh mesh = BuildMesh(
                        visible[i], out int chunkTriangles, out int chunkArchitecturalTriangles);
                    triangles += chunkTriangles;
                    architecturalTriangles += chunkArchitecturalTriangles;

                    var root = new GameObject($"CI Kentridge Unified {visible[i].Coordinate}");
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    root.AddComponent<MeshRenderer>().sharedMaterials = palette;
                    objects.Add(root);
                    meshes.Add(mesh);
                }

                if (architecturalTriangles == 0)
                    throw new InvalidOperationException(
                        "Kentridge unified extraction produced no faceted architectural triangles.");

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Kentridge Unified Capture",
                    antiAliasing = 4,
                };
                target.Create();
                image = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                camera.targetTexture = target;

                var viewMetadata = new List<string>(Views.Length);
                for (int i = 0; i < Views.Length; i++)
                {
                    ConfigureCamera(
                        camera, Views[i], focus, spanMetres, overviewDistance,
                        centreDmX, centreDmZ, centreSurfaceY);
                    Capture(camera, target, image,
                        Path.Combine(outputDirectory, "kentridge-" + Views[i].Name + ".png"));
                    viewMetadata.Add(
                        $"view={Views[i].Name} camera={camera.transform.position:F2} " +
                        $"rotation={camera.transform.eulerAngles:F2} fov={camera.fieldOfView:F1}");
                }

                string metadata =
                    $"capture=unified-terrain-and-architecture\n" +
                    $"seed={Seed}\n" +
                    $"plots={plan.Plots.Count}\n" +
                    $"streets={plan.Streets.Count}\n" +
                    $"featureInstances={featureInstances}\n" +
                    $"featureVoxels={featureVoxels}\n" +
                    $"surfaceChunks={visible.Count}\n" +
                    $"surfaceTriangles={triangles}\n" +
                    $"architecturalTriangles={architecturalTriangles}\n" +
                    $"knownChunks={cache.KnownCount}\n" +
                    $"residentChunks={cache.ResidentCount}\n" +
                    $"boundsDm={minX},{minZ}..{maxX},{maxZ}\n" +
                    $"captures={Views.Length}\n" +
                    string.Join("\n", viewMetadata) + "\n";
                File.WriteAllText(
                    Path.Combine(outputDirectory, "kentridge-overview.txt"), metadata);
                Debug.Log($"CI Kentridge unified captures written to {outputDirectory}\n{metadata}");
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
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                for (int i = 0; i < objects.Count; i++)
                    if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
                for (int i = 0; i < meshes.Count; i++)
                    if (meshes[i] != null) UnityEngine.Object.DestroyImmediate(meshes[i]);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                DestroyPalette(palette);
                cache?.Dispose();
                if (catalogue.IsCreated) catalogue.Dispose();
                if (table.IsCreated) table.Dispose();
                if (pool.IsCreated) pool.Dispose();
            }
        }

        private static void LoadTerrain(
            int minX, int maxX, int minZ, int maxZ,
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

        private static void ConfigureCamera(
            Camera camera, View view, Vector3 focus,
            float spanMetres, float overviewDistance,
            int centreDmX, int centreDmZ, float centreSurfaceY)
        {
            if (!view.Street)
            {
                camera.fieldOfView = 39f;
                camera.transform.position =
                    focus + view.Direction * overviewDistance + Vector3.up * (overviewDistance * 0.62f);
                camera.transform.LookAt(focus);
                camera.farClipPlane = overviewDistance * 3.5f;
                return;
            }

            float horizontalDistance = Mathf.Max(52f, spanMetres * 0.43f);
            Vector3 offset = view.Direction * horizontalDistance;
            int cameraDmX = centreDmX + Mathf.RoundToInt(offset.x / VoxelSize);
            int cameraDmZ = centreDmZ + Mathf.RoundToInt(offset.z / VoxelSize);
            float cameraSurfaceY = AuthoredSurfaceYMetres(cameraDmX, cameraDmZ);

            camera.fieldOfView = 52f;
            camera.transform.position = new Vector3(
                focus.x + offset.x,
                cameraSurfaceY + 3.4f,
                focus.z + offset.z);
            camera.transform.LookAt(new Vector3(
                focus.x, centreSurfaceY + 5.2f, focus.z));
            camera.farClipPlane = Mathf.Max(240f, spanMetres * 2.2f);
        }

        private static float AuthoredSurfaceYMetres(int xDm, int zDm)
        {
            int natural = TerrainSampler.HeightAt(xDm, zDm, Seed);
            int authored = KentridgeVerticalProfile.SurfaceYAtDm(xDm, zDm, Seed, 1);
            return Math.Max(natural, authored) * VoxelSize;
        }

        private static void Capture(
            Camera camera, RenderTexture target, Texture2D image, string path)
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

        private static Mesh BuildMesh(
            CpuTransvoxelChunkCache.Entry entry,
            out int triangleCount,
            out int architecturalTriangleCount)
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

            architecturalTriangleCount = 0;
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
                if (architectural) architecturalTriangleCount++;

                groups[group].Add((int)sourceIndices[i]);
                groups[group].Add((int)sourceIndices[i + 1]);
                groups[group].Add((int)sourceIndices[i + 2]);
            }

            var mesh = new Mesh
            {
                name = $"CI Kentridge Unified {entry.Coordinate}",
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                normals = normals,
                subMeshCount = PresentationCount,
            };
            for (int i = 0; i < groups.Length; i++)
                mesh.SetTriangles(groups[i], i, false);
            mesh.RecalculateBounds();
            triangleCount = sourceIndices.Length / 3;
            return mesh;
        }

        private static MaterialPalette BuildMaterialPalette()
        {
            MaterialPalette result = default;
            for (byte material = 1; material < MaterialCount; material++)
            {
                result.Register(
                    material, 128, DestructionClass.Crumble,
                    SurfaceStyles.Smooth, uint.MaxValue);
            }
            return result;
        }

        private static List<int3> SurfaceChunkSeeds(
            int minX, int maxX, int minZ, int maxZ)
        {
            int edge = CpuTransvoxelChunkCache.BaseVoxelsPerAxis;
            int minChunkX = FloorDiv(minX, edge) - 1;
            int maxChunkX = FloorDiv(maxX, edge) + 1;
            int minChunkZ = FloorDiv(minZ, edge) - 1;
            int maxChunkZ = FloorDiv(maxZ, edge) + 1;
            int maxChunkY = FloorDiv(TerrainSampler.MaxHeight, edge);
            var result = new List<int3>();

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

        private static Material[] BuildPresentationPalette(Shader shader)
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
                result[MaterialCount + i] =
                    NewMaterial(shader, $"CI Kentridge Architectural {i}", hard[i]);
            return result;
        }

        private static Color[] BaseColours()
        {
            var colours = new Color[MaterialCount];
            for (int i = 0; i < colours.Length; i++)
                colours[i] = new Color(0.55f, 0.55f, 0.55f, 1f);
            return colours;
        }

        private static Shader FindPreviewShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            return shader != null
                ? shader
                : throw new InvalidOperationException("No CI preview shader found.");
        }

        private static Material NewMaterial(Shader shader, string name, Color colour)
        {
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", colour);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0f);
            return material;
        }

        private static void DestroyPalette(Material[] palette)
        {
            if (palette == null) return;
            for (int i = 0; i < palette.Length; i++)
                if (palette[i] != null)
                    UnityEngine.Object.DestroyImmediate(palette[i]);
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

        private static void TownBounds(
            SettlementPlan plan,
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
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }
    }
}
