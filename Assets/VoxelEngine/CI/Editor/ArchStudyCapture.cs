using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Structures.Runtime;
using VoxelEngine.Storage.Runtime;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

using VoxelEngine.Structures.Api;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Isolated arch look-development rendered through the production surface path.
    ///
    /// The feature/arch-system study established the composition, deterministic weathering and
    /// variant workflow. This master version deliberately rebuilds the study from the canonical
    /// ArchFeatureDefinition and per-cell surface semantics; it does not restore the retired
    /// hard-surface renderer or use moss as a structural material.
    /// </summary>
    public static class ArchStudyCapture
    {
        private const int Width = 1600;
        private const int Height = 700;
        private const int HeroSize = 1000;
        private const float VoxelSize = 0.1f;
        private const uint Seed = 0xA341u;

        private readonly struct Variant
        {
            public readonly int ClearSpan;
            public readonly int PierHeight;
            public readonly int RingThickness;
            public readonly int Voussoirs;
            public readonly byte MossCoverage;
            public readonly uint SeedOffset;
            public readonly byte Material;
            public readonly ArchRuinDamage Damage;
            public readonly byte DamageScale;

            public Variant(int clearSpan, int pierHeight, int ringThickness, int voussoirs,
                           byte mossCoverage, uint seedOffset, byte material,
                           ArchRuinDamage damage, byte damageScale)
            {
                ClearSpan = clearSpan;
                PierHeight = pierHeight;
                RingThickness = ringThickness;
                Voussoirs = voussoirs;
                MossCoverage = mossCoverage;
                SeedOffset = seedOffset;
                Material = material;
                Damage = damage;
                DamageScale = damageScale;
            }
        }

        public static void RunVariants()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "ArchStudy");
            Directory.CreateDirectory(outputDirectory);

            RegionTable table = default;
            BrickPool pool = default;
            GameObject cameraObject = null;
            GameObject keyObject = null;
            RenderTexture target = null;
            RenderTexture heroTarget = null;
            Texture2D capture = null;
            Texture2D heroCapture = null;
            Texture2D heroNormalsCapture = null;

            var previousSource = VoxelRenderBridge.Source;
            var previousChanges = VoxelRenderBridge.Changes;
            Color previousTint = VoxelRenderBridge.SurfaceDebugTint;
            Vector3 previousSun = VoxelRenderBridge.SunDirection;
            Color previousHorizon = VoxelRenderBridge.SkyHorizon;
            Color previousZenith = VoxelRenderBridge.SkyZenith;
            double previousSolidBudget = VoxelRenderBridge.SolidBuildBudgetMs;
            double previousWaterBudget = VoxelRenderBridge.WaterBuildBudgetMs;

            try
            {
                table = new RegionTable(16, Allocator.Persistent);
                pool = new BrickPool(32_768, Allocator.Persistent);

                MaterialPalette palette = default;
                const uint weatherCoatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                            | (1u << Coatings.Soot) | (1u << Coatings.Wet);
                palette.Register(Mat.MasonrySmall, 205, DestructionClass.Crumble,
                                 SurfaceStyles.MasonryJoint, weatherCoatings);
                palette.Register(Mat.MasonryMedium, 210, DestructionClass.Crumble,
                                 SurfaceStyles.MasonryJoint, weatherCoatings);
                palette.Register(Mat.MasonryLarge, 218, DestructionClass.Crumble,
                                 SurfaceStyles.MasonryJoint, weatherCoatings);
                SurfaceCatalogue surfaces = SurfaceCatalogue.CreateBuiltIns();
                CoatingCatalogue coatings = CoatingCatalogue.CreateBuiltIns();
                var profileBlocks = new ProfileBlockStore();

                Variant[] variants =
                {
                    new(24, 32, 6, 11, 90, 0x1111u, Mat.MasonrySmall,
                        ArchRuinDamage.Intact, 1),
                    new(32, 40, 7, 13, 115, 0x2222u, Mat.MasonryMedium,
                        ArchRuinDamage.Intact, 2),
                    new(40, 44, 8, 15, 145, 0x3333u, Mat.MasonryLarge,
                        ArchRuinDamage.CollapsedShoulder, 3),
                    new(28, 36, 6, 13, 170, 0x4444u, Mat.MasonryMedium,
                        ArchRuinDamage.BrokenCrown, 2),
                };

                const int spacing = 108;
                int firstCentreX = -spacing * (variants.Length - 1) / 2;
                int tallest = 0;
                int totalVoxels = 0;
                int chipped = 0;
                int mossed = 0;
                int captureFrames = 0;
                long convergenceMilliseconds = 0;
                VoxelSurfaceMetrics finalMetrics = default;

                for (int i = 0; i < variants.Length; i++)
                {
                    Variant variant = variants[i];
                    var arch = new ArchFeatureDefinition
                    {
                        ClearSpan = variant.ClearSpan,
                        PierHeight = variant.PierHeight,
                        RingThickness = variant.RingThickness,
                        Depth = 12,
                        VoussoirCount = variant.Voussoirs,
                        JointRecessDepth = 1,
                        StoneMaterial = variant.Material,
                        PierStyle = SurfaceStyles.MasonryJoint,
                        RingStyle = SurfaceStyles.MasonryJoint,
                        // Weathering below applies sparse coating metadata. Coating every solid
                        // cell here would make moss a uniform paint layer rather than growth.
                        Coating = Coatings.None,
                    };

                    var bay = new ArchBayFeatureDefinition
                    {
                        Arch = arch,
                        ShoulderWidth = 10,
                        TopMargin = 8,
                        FaceRecess = 1,
                        PlinthHeight = 4,
                        ImpostHeight = 3,
                        Damage = variant.Damage,
                        DamageSeed = Seed + variant.SeedOffset,
                        DamageScale = variant.DamageScale,
                    };
                    ArchValidationError validation = bay.Validate(in palette, in surfaces,
                                                                   in coatings);
                    if (validation != ArchValidationError.None)
                        throw new InvalidOperationException($"Invalid arch variant {i}: {validation}");

                    int centreX = firstCentreX + i * spacing;
                    int3 origin = new(centreX - bay.Width / 2, 8, 0);
                    var primitives = new NativeList<Primitive>(bay.Metadata.MaxPrimitives,
                                                               Allocator.Temp);
                    try
                    {
                        if (!bay.Emit(origin, primitives, profileBlocks))
                            throw new InvalidOperationException($"Arch variant {i} did not emit.");
                        int3 max = origin + bay.Metadata.Footprint;
                        var reads = new RegionReadSource(in table, in pool);
                        var mutations = new RegionMutationStore(in table, in pool);
                        RasterResult result = PrimitiveRasteriser.Rasterise(
                            primitives.AsArray(), origin, max, reads, mutations);
                        if (result.BudgetExceeded)
                            throw new InvalidOperationException($"Arch variant {i} exceeded budget.");
                        totalVoxels += result.VoxelsWritten;

                        var brush = new VoxelBrush(reads, mutations, palette, 2_000_000);
                        int3 weatherMin = origin - new int3(2, 2, 2);
                        int3 weatherSize = bay.Metadata.Footprint + new int3(4, 4, 4);
                        chipped += MasonryWeathering.ChipExposedEdges(
                            ref brush, weatherMin, weatherSize, Seed + variant.SeedOffset,
                            severity: 0, protectedBaseLayers: 0);
                        mossed += MasonryWeathering.CoatExposedSurfaces(
                            ref brush, weatherMin, weatherSize, Coatings.Moss,
                            Seed + variant.SeedOffset, variant.MossCoverage, dripPasses: 0);
                    }
                    finally
                    {
                        primitives.Dispose();
                    }

                    tallest = math.max(tallest, bay.Height);
                }

                var changes = new VoxelChangeJournal();
                using (NativeArray<int3> regions = table.GetResidentCoords(Allocator.Temp))
                    for (int i = 0; i < regions.Length; i++)
                        changes.PublishRegion(regions[i]);

                cameraObject = new GameObject("Arch Variants Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                Vector3 focus = new(0f, (8 + tallest * 0.48f) * VoxelSize, 0f);
                camera.transform.position = focus + new Vector3(0f, 0.8f, -35f);
                camera.transform.LookAt(focus);
                camera.fieldOfView = 32f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 60f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.46f, 0.48f, 1f);
                camera.allowHDR = false;
                camera.enabled = false;

                keyObject = new GameObject("Arch Variants Key");
                Light key = keyObject.AddComponent<Light>();
                key.type = LightType.Directional;
                key.color = new Color(1.00f, 0.95f, 0.82f);
                key.intensity = 1.25f;
                keyObject.transform.rotation = Quaternion.Euler(38f, -34f, 0f);

                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.34f, 0.42f, 0.58f);
                RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.40f);
                RenderSettings.ambientGroundColor = new Color(0.20f, 0.20f, 0.22f);

                VoxelRenderBridge.SurfaceDebugTint = Color.white;
                VoxelRenderBridge.SkyZenith = new Color(0.341f, 0.600f, 0.847f, 1f);
                VoxelRenderBridge.SkyHorizon = new Color(0.627f, 0.722f, 0.773f, 1f);
                VoxelRenderBridge.SunDirection = -keyObject.transform.forward;
                VoxelRenderBridge.Changes = changes;
                // Spend CPU on extraction rather than thousands of redundant full-resolution
                // presentation frames. This is the same production scheduler and extractor.
                VoxelRenderBridge.SolidBuildBudgetMs = 12.0;
                VoxelRenderBridge.WaterBuildBudgetMs = 2.0;
                var readSource = new RegionReadSource(in table, in pool, changes);
                VoxelRenderBridge.Source = () => new VoxelWorldView
                {
                    Storage = readSource,
                    Palette = palette,
                    SurfaceCatalogueView = surfaces,
                    CoatingCatalogueView = coatings,
                    ProfileBlocks = profileBlocks,
                };

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Arch Variants Capture",
                    antiAliasing = 4,
                };
                target.Create();
                camera.targetTexture = target;

                RenderTexture previousActive = RenderTexture.active;
                try
                {
                    // Observe production scheduler convergence rather than guessing a frame
                    // count. The previous fixed 4,096 full-resolution renders spent five minutes
                    // redrawing an already-finished mesh after richer moss made each frame more
                    // expensive. This does not raise the gameplay budget or introduce a fixture
                    // mesher; it simply stops when the shipping scheduler reports no work left.
                    var convergenceWatch = System.Diagnostics.Stopwatch.StartNew();
                    int stableFrames = 0;
                    const int maxConvergenceFrames = 512;
                    for (; captureFrames < maxConvergenceFrames; captureFrames++)
                    {
                        camera.Render();
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        bool discovered = metrics.SolidKnownChunks > 0;
                        bool converged = discovered && metrics.SolidDirtyChunks == 0
                            && metrics.SolidResidentChunks >= metrics.SolidKnownChunks;
                        stableFrames = converged ? stableFrames + 1 : 0;
                        if (stableFrames >= 3) break;
                    }
                    convergenceWatch.Stop();
                    convergenceMilliseconds = convergenceWatch.ElapsedMilliseconds;
                    if (captureFrames >= maxConvergenceFrames)
                    {
                        VoxelSurfaceMetrics metrics = VoxelRenderBridge.SurfaceMetrics;
                        throw new InvalidOperationException(
                            $"Arch capture did not converge: known={metrics.SolidKnownChunks}, " +
                            $"resident={metrics.SolidResidentChunks}, dirty={metrics.SolidDirtyChunks}.");
                    }
                    captureFrames++;
                    finalMetrics = VoxelRenderBridge.SurfaceMetrics;
                    RenderTexture.active = target;
                    capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                    capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    capture.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(outputDirectory, "arch-variants.png"),
                                       capture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    camera.targetTexture = null;
                }

                // A close study is part of the artifact, not a separate fixture renderer. It
                // reuses the converged production mesh and only changes the camera framing so
                // silhouette, joints, coating breakup and texture scale can be judged at size.
                int heroCentreX = firstCentreX + spacing;
                Variant heroVariant = variants[1];
                int heroHeight = heroVariant.PierHeight + heroVariant.ClearSpan / 2 + 8;
                Vector3 heroFocus = new(heroCentreX * VoxelSize,
                    (8 + heroHeight * 0.5f) * VoxelSize, 0.45f);
                camera.transform.position = heroFocus + new Vector3(3.8f, 1.1f, -14.5f);
                camera.transform.LookAt(heroFocus);
                camera.fieldOfView = 34f;
                heroTarget = new RenderTexture(HeroSize, HeroSize, 24,
                    RenderTextureFormat.ARGB32)
                {
                    name = "Arch Hero Capture",
                    antiAliasing = 4,
                };
                heroTarget.Create();
                camera.targetTexture = heroTarget;
                previousActive = RenderTexture.active;
                try
                {
                    camera.Render();
                    RenderTexture.active = heroTarget;
                    heroCapture = new Texture2D(HeroSize, HeroSize,
                        TextureFormat.RGBA32, false, false);
                    heroCapture.ReadPixels(new Rect(0, 0, HeroSize, HeroSize), 0, 0, false);
                    heroCapture.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(outputDirectory, "arch-hero.png"),
                                       heroCapture.EncodeToPNG());

                    VoxelRenderBridge.SurfaceDebugTint = Color.gray;
                    camera.Render();
                    RenderTexture.active = heroTarget;
                    heroNormalsCapture = new Texture2D(HeroSize, HeroSize,
                        TextureFormat.RGBA32, false, false);
                    heroNormalsCapture.ReadPixels(new Rect(0, 0, HeroSize, HeroSize), 0, 0, false);
                    heroNormalsCapture.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(outputDirectory, "arch-hero-normals.png"),
                                       heroNormalsCapture.EncodeToPNG());
                    VoxelRenderBridge.SurfaceDebugTint = Color.white;
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    camera.targetTexture = null;
                }

                string metadata =
                    $"seed={Seed}\n" +
                    $"variants={variants.Length}\n" +
                    $"baseMaterials={Mat.MasonrySmall},{Mat.MasonryMedium},{Mat.MasonryLarge}\n" +
                    $"surfaceStyle={SurfaceStyles.MasonryJoint}\n" +
                    $"coating={Coatings.Moss}\n" +
                    $"voxelsWritten={totalVoxels}\n" +
                    $"voxelsChipped={chipped}\n" +
                    $"voxelsMossed={mossed}\n";
                metadata += $"captureFrames={captureFrames}\n" +
                            $"convergenceMilliseconds={convergenceMilliseconds}\n" +
                            $"residentGeometryBytes={finalMetrics.ResidentGeometryBytes}\n" +
                            $"uploadedGeometryBytes={finalMetrics.UploadedGeometryBytes}\n" +
                            $"decorationClumps={finalMetrics.SolidDecorationClumps}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "arch-variants.txt"), metadata);
                Debug.Log($"Master arch variants written to {outputDirectory}\n{metadata}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                VoxelRenderBridge.Source = previousSource;
                VoxelRenderBridge.Changes = previousChanges;
                VoxelRenderBridge.SurfaceDebugTint = previousTint;
                VoxelRenderBridge.SunDirection = previousSun;
                VoxelRenderBridge.SkyHorizon = previousHorizon;
                VoxelRenderBridge.SkyZenith = previousZenith;
                VoxelRenderBridge.SolidBuildBudgetMs = previousSolidBudget;
                VoxelRenderBridge.WaterBuildBudgetMs = previousWaterBudget;
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (heroCapture != null) UnityEngine.Object.DestroyImmediate(heroCapture);
                if (heroNormalsCapture != null)
                    UnityEngine.Object.DestroyImmediate(heroNormalsCapture);
                if (target != null)
                {
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
                if (heroTarget != null)
                {
                    heroTarget.Release();
                    UnityEngine.Object.DestroyImmediate(heroTarget);
                }
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (keyObject != null) UnityEngine.Object.DestroyImmediate(keyObject);
                if (pool.IsCreated) pool.Dispose();
                if (table.IsCreated) table.Dispose();
            }
        }
    }
}

namespace VoxelEngine.CI
{
    /// <summary>
    /// Masonry indices for the arch study capture. Mirrors Game.Materials.Api.GameMaterialIds;
    /// duplicated because this is an engine assembly and EngineGameDependencyBoundaryTests
    /// forbids a Game dependency from anything under Assets/VoxelEngine.
    /// </summary>
    internal static class Mat
    {
        internal const byte MasonrySmall = 18;
        internal const byte MasonryMedium = 19;
        internal const byte MasonryLarge = 20;
    }
}
