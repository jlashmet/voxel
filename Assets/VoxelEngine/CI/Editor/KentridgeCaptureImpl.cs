using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static partial class KentridgeCapture
    {
        private sealed class WorldState : IDisposable
        {
            public RegionTable Table;
            public BrickPool Pool;
            public MaterialPalette Palette;
            public SurfaceCatalogue Surfaces;
            public CoatingCatalogue Coatings;
            public ProfileBlockStore Profiles;
            public VoxelChangeJournal Changes;

            public VoxelWorldView View() => new()
            {
                Table = Table,
                Pool = Pool,
                Palette = Palette,
                SurfaceCatalogue = Surfaces,
                CoatingCatalogue = Coatings,
                ProfileBlocks = Profiles,
            };

            public void Dispose()
            {
                if (Table.IsCreated) Table.Dispose();
                if (Pool.IsCreated) Pool.Dispose();
            }
        }

        private static WorldState BuildWorld()
        {
            var world = new WorldState
            {
                Table = new RegionTable(8, Allocator.Persistent),
                Pool = new BrickPool(48_000, Allocator.Persistent),
                Palette = default,
                Surfaces = SurfaceCatalogue.CreateBuiltIns(),
                Coatings = CoatingCatalogue.CreateBuiltIns(),
                Profiles = new ProfileBlockStore(),
                Changes = new VoxelChangeJournal(),
            };
            ConfigurePalette(ref world.Palette);

            var primitiveList = new NativeList<Primitive>(256, Allocator.Temp);
            try
            {
                BuildArchitecture(primitiveList, world.Profiles);
                int3 min = new(-150, 0, -20);
                int3 max = new(170, 170, 170);
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitiveList.AsArray(), min, max, ref world.Table, ref world.Pool);
                if (result.BudgetExceeded)
                    throw new InvalidOperationException("Kentridge capture exceeded primitive budget.");
            }
            finally
            {
                primitiveList.Dispose();
            }

            using NativeArray<int3> regions = world.Table.GetResidentCoords(Allocator.Temp);
            for (int i = 0; i < regions.Length; i++) world.Changes.PublishRegion(regions[i]);
            return world;
        }

        private static void ConfigurePalette(ref MaterialPalette palette)
        {
            const uint stoneCoatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                      | (1u << Coatings.Soot) | (1u << Coatings.Wet);
            palette.Register(1, 210, DestructionClass.Crumble,
                             SurfaceStyles.MasonryJoint, stoneCoatings);
            palette.Register(2, 210, DestructionClass.Crumble,
                             SurfaceStyles.Rounded, stoneCoatings);
            palette.Register(3, 95, DestructionClass.Splinter,
                             SurfaceStyles.Planar, 1u << Coatings.Wet);
            palette.Register(4, 25, DestructionClass.Powder,
                             SurfaceStyles.Smooth, 1u << Coatings.Moss);
            palette.Register(5, 10, DestructionClass.Powder,
                             SurfaceStyles.Sharp, 1u << Coatings.Wet);
        }

        private static void BuildArchitecture(NativeList<Primitive> output,
                                              ProfileBlockStore profiles)
        {
            int order = 0;
            byte stone = 1;
            byte rounded = 2;
            byte wood = 3;

            // Ground slab and stepped foundation.
            output.Add(Box(new int3(-120, 0, 0), new int3(240, 5, 130), stone,
                           SurfaceStyles.MasonryJoint, order++));
            output.Add(Box(new int3(-105, 5, 8), new int3(210, 5, 116), stone,
                           SurfaceStyles.MasonryJoint, order++));
            output.Add(Box(new int3(-92, 10, 16), new int3(184, 6, 102), rounded,
                           SurfaceStyles.Rounded, order++));

            // Main hall mass, front facade toward -Z.
            output.Add(Box(new int3(-78, 16, 25), new int3(156, 82, 76), stone,
                           SurfaceStyles.MasonryJoint, order++));
            output.Add(Box(new int3(-70, 22, 18), new int3(140, 66, 8), rounded,
                           SurfaceStyles.Rounded, order++));

            // Recessed central entry arch — proud voussoirs over the facade.
            var entry = new ArchFeatureDefinition
            {
                ClearSpan = 38,
                PierHeight = 38,
                RingThickness = 8,
                Depth = 10,
                VoussoirCount = 13,
                JointRecessDepth = 1,
                StoneMaterial = stone,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            entry.Emit(new int3(-27, 16, 8), output, profiles);

            // Carve the actual doorway through the facade/backing.
            Primitive entryCarve = CurvedPrimitiveEmitter.Annulus(
                new int3(-1, 54, 28), 20, 0, 28, 2, true,
                stone, SurfaceStyles.MasonryJoint, PrimitiveMode.Carve, order++);
            output.Add(entryCarve);
            output.Add(Box(new int3(-19, 16, 14), new int3(38, 39, 30), stone,
                           SurfaceStyles.MasonryJoint, order++, PrimitiveMode.Carve));

            // Paired side towers with rounded vertical arrises.
            AddTower(output, new int3(-102, 10, 18), 31, 96, stone, rounded, ref order);
            AddTower(output, new int3(71, 10, 18), 31, 96, stone, rounded, ref order);

            // Upper central tower and roofline.
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                new int3(-36, 86, 38), new int3(72, 62, 54), 5,
                stone, SurfaceStyles.MasonryJoint, PrimitiveMode.Fill, order++));
            AddBattlements(output, new int3(-39, 148, 34), new int3(78, 8, 62),
                           stone, ref order);

            // Long clerestory windows and balcony beam.
            for (int x = -55; x <= 55; x += 22)
            {
                output.Add(CurvedPrimitiveEmitter.RoundedBox(
                    new int3(x - 4, 68, 17), new int3(8, 22, 7), 3,
                    stone, SurfaceStyles.Rounded, PrimitiveMode.Carve, order++));
                output.Add(CurvedPrimitiveEmitter.RoundedBox(
                    new int3(x - 3, 70, 18), new int3(6, 18, 4), 2,
                    5, SurfaceStyles.Sharp, PrimitiveMode.Fill, order++));
            }
            output.Add(Box(new int3(-70, 60, 12), new int3(140, 5, 14), wood,
                           SurfaceStyles.Planar, order++));

            // Buttresses / vertical rhythm on the facade.
            for (int x = -72; x <= 72; x += 24)
            {
                if (math.abs(x) < 22) continue;
                output.Add(CurvedPrimitiveEmitter.RoundedBox(
                    new int3(x - 3, 16, 14), new int3(6, 54, 12), 2,
                    rounded, SurfaceStyles.Rounded, PrimitiveMode.Fill, order++));
            }

            // Roof slabs set behind the parapet.
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                new int3(-74, 98, 31), new int3(148, 8, 64), 3,
                rounded, SurfaceStyles.Rounded, PrimitiveMode.Fill, order++));
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                new int3(-32, 148, 42), new int3(64, 9, 44), 3,
                rounded, SurfaceStyles.Rounded, PrimitiveMode.Fill, order++));

            // Courtyard garden blocks and reflecting pool in front/right.
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                new int3(38, 7, -6), new int3(44, 6, 18), 4,
                4, SurfaceStyles.Smooth, PrimitiveMode.Fill, order++, Coatings.Moss));
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                new int3(47, 9, -12), new int3(26, 2, 8), 2,
                5, SurfaceStyles.Sharp, PrimitiveMode.Fill, order++));
        }

        private static void AddTower(NativeList<Primitive> output, int3 min,
                                     int width, int height, byte stone, byte rounded,
                                     ref int order)
        {
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                min, new int3(width, height, 48), 5,
                stone, SurfaceStyles.MasonryJoint, PrimitiveMode.Fill, order++));
            output.Add(CurvedPrimitiveEmitter.RoundedBox(
                min + new int3(4, height - 25, -3), new int3(width - 8, 22, 8), 3,
                rounded, SurfaceStyles.Rounded, PrimitiveMode.Fill, order++));
            AddBattlements(output, min + new int3(-3, height, -3),
                           new int3(width + 6, 8, 54), stone, ref order);
        }

        private static void AddBattlements(NativeList<Primitive> output, int3 min,
                                           int3 size, byte material, ref int order)
        {
            output.Add(Box(min, new int3(size.x, 3, size.z), material,
                           SurfaceStyles.MasonryJoint, order++));
            const int merlon = 7;
            const int gap = 5;
            int period = merlon + gap;
            for (int x = 0; x < size.x; x += period)
            {
                int width = math.min(merlon, size.x - x);
                output.Add(Box(min + new int3(x, 3, 0), new int3(width, 5, 5), material,
                               SurfaceStyles.MasonryJoint, order++));
                output.Add(Box(min + new int3(x, 3, size.z - 5),
                               new int3(width, 5, 5), material,
                               SurfaceStyles.MasonryJoint, order++));
            }
            for (int z = 0; z < size.z; z += period)
            {
                int depth = math.min(merlon, size.z - z);
                output.Add(Box(min + new int3(0, 3, z), new int3(5, 5, depth), material,
                               SurfaceStyles.MasonryJoint, order++));
                output.Add(Box(min + new int3(size.x - 5, 3, z),
                               new int3(5, 5, depth), material,
                               SurfaceStyles.MasonryJoint, order++));
            }
        }

        private static Primitive Box(int3 min, int3 size, byte material,
                                     ushort style, int order,
                                     PrimitiveMode mode = PrimitiveMode.Fill)
        {
            Primitive p = BoxEmitter.Box(min, size, material, mode, order, style);
            p.SurfaceFlags = style == SurfaceStyles.MasonryJoint
                ? VoxelSurfaceFlags.PreserveFeature : VoxelSurfaceFlags.None;
            return p;
        }

        private static void ConfigurePresentation(bool mossEnabled, float mossStrength)
        {
            VoxelRenderBridge.SunDirection = new Vector3(-0.42f, 0.79f, -0.45f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.67f, 0.74f, 0.78f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.34f, 0.54f, 0.74f, 1f);
            VoxelRenderBridge.SurfaceDebugTint = Color.white;
            VoxelRenderBridge.FarFieldEnabled = false;
            VoxelRenderBridge.SolidBuildBudgetMs = 8.0;
            VoxelRenderBridge.WaterBuildBudgetMs = 0.0;

            VoxelPresentationCatalogue.MaterialAlbedo[1] =
                new Vector4(0.58f, 0.54f, 0.47f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[2] =
                new Vector4(0.47f, 0.45f, 0.41f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[3] =
                new Vector4(0.23f, 0.14f, 0.08f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[4] =
                new Vector4(0.22f, 0.38f, 0.12f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[5] =
                new Vector4(0.34f, 0.61f, 0.73f, 1f);
            VoxelPresentationCatalogue.CoatingTint[Coatings.Moss] = mossEnabled
                ? new Vector4(0.20f, 0.42f, 0.12f, mossStrength)
                : Vector4.zero;
        }

        private static string Capture(string outputDirectory, WorldState world,
                                      string label, bool mossEnabled, float mossStrength,
                                      Action<Camera> positionCamera)
        {
            Directory.CreateDirectory(outputDirectory);
            ConfigurePresentation(mossEnabled, mossStrength);
            VoxelRenderBridge.Changes = world.Changes;
            VoxelRenderBridge.Source = world.View;

            var go = new GameObject("Kentridge capture camera");
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.42f, 0.48f, 0.52f, 1f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 140f;
            camera.fieldOfView = 38f;
            camera.allowHDR = false;
            positionCamera(camera);

            try
            {
                // Force a deterministic warm-up of the scheduler through normal Prepare calls.
                // The editor capture loop drives it by rendering until no geometry remains dirty.
                const int width = 1024;
                const int height = 768;
                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture oldTarget = camera.targetTexture;
                try
                {
                    rt.Create();
                    camera.targetTexture = rt;
                    for (int frame = 0; frame < 240; frame++)
                    {
                        camera.Render();
                        if (VoxelRenderBridge.SurfaceMetrics.SolidDirtyChunks == 0
                            && VoxelRenderBridge.SurfaceMetrics.SolidResidentChunks > 0)
                            break;
                    }
                    RenderTexture.active = rt;
                    camera.Render();
                    image.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    image.Apply(false, false);
                    string path = Path.Combine(outputDirectory, label + ".png");
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    return path;
                }
                finally
                {
                    camera.targetTexture = oldTarget;
                    RenderTexture.active = previous;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(image);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                VoxelRenderBridge.Source = null;
                VoxelRenderBridge.Changes = null;
            }
        }
    }
}