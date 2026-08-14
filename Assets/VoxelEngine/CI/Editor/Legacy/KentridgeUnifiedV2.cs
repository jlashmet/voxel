using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Features.Emitters;
using VoxelEngine.Core.Storage;
using VoxelEngine.Storage.Api;
using VoxelEngine.Rendering;

namespace VoxelEngine.CI
{
    internal static partial class KentridgeUnifiedV2
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
                Pool = new BrickPool(96_000, Allocator.Persistent),
                Palette = default,
                Surfaces = SurfaceCatalogue.CreateBuiltIns(),
                Coatings = CoatingCatalogue.CreateBuiltIns(),
                Profiles = new ProfileBlockStore(),
                Changes = new VoxelChangeJournal(),
            };
            ConfigurePalette(ref world.Palette);

            var primitives = new NativeList<Primitive>(512, Allocator.Temp);
            try
            {
                BuildScene(primitives, world.Profiles);
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), new int3(-170, -10, -80), new int3(190, 190, 210),
                    ref world.Table, ref world.Pool);
                if (result.BudgetExceeded)
                    throw new InvalidOperationException("Kentridge v2 exceeded primitive budget.");
            }
            finally
            {
                primitives.Dispose();
            }

            using NativeArray<int3> regions = world.Table.GetResidentCoords(Allocator.Temp);
            for (int i = 0; i < regions.Length; i++) world.Changes.PublishRegion(regions[i]);
            return world;
        }

        private static void ConfigurePalette(ref MaterialPalette palette)
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                               | (1u << Coatings.Soot) | (1u << Coatings.Wet);
            palette.Register(1, 210, DestructionClass.Crumble, SurfaceStyles.MasonryJoint, weather);
            palette.Register(2, 205, DestructionClass.Crumble, SurfaceStyles.Rounded, weather);
            palette.Register(3, 200, DestructionClass.Crumble, SurfaceStyles.Planar, weather);
            palette.Register(4, 95, DestructionClass.Splinter, SurfaceStyles.Planar, 1u << Coatings.Wet);
            palette.Register(5, 25, DestructionClass.Powder, SurfaceStyles.Smooth, 1u << Coatings.Moss);
            palette.Register(6, 12, DestructionClass.Powder, SurfaceStyles.Sharp, 1u << Coatings.Wet);
        }

        private static void BuildScene(NativeList<Primitive> output, ProfileBlockStore profiles)
        {
            int order = 0;
            BuildTerrain(output, ref order);
            BuildWestKeep(output, profiles, ref order);
            BuildGatehouse(output, profiles, ref order);
            BuildEastTower(output, ref order);
            BuildWallsAndCourtyard(output, ref order);
            BuildGarden(output, ref order);
        }

        private static Primitive Box(int3 min, int3 size, byte material, ushort style,
                                     ref int order, PrimitiveMode mode = PrimitiveMode.Fill,
                                     byte coating = Coatings.None)
        {
            Primitive p = BoxEmitter.Box(min, size, material, mode, order++, style, coating);
            p.SurfaceFlags = style == SurfaceStyles.MasonryJoint
                ? VoxelSurfaceFlags.PreserveFeature : VoxelSurfaceFlags.None;
            return p;
        }

        private static Primitive RoundedBox(int3 min, int3 size, int radius, byte material,
                                            ushort style, ref int order,
                                            PrimitiveMode mode = PrimitiveMode.Fill,
                                            byte coating = Coatings.None)
        {
            Primitive p = CurvedPrimitiveEmitter.RoundedBox(
                min, size, radius, material, style, mode, order++, coating);
            if (style == SurfaceStyles.MasonryJoint)
                p.SurfaceFlags |= VoxelSurfaceFlags.PreserveFeature;
            return p;
        }

        private static void ConfigurePresentation(bool mossEnabled, float mossStrength)
        {
            VoxelRenderBridge.SunDirection = new Vector3(-0.55f, 0.74f, -0.38f).normalized;
            VoxelRenderBridge.SkyHorizon = new Color(0.64f, 0.74f, 0.80f, 1f);
            VoxelRenderBridge.SkyZenith = new Color(0.34f, 0.55f, 0.73f, 1f);
            VoxelRenderBridge.SurfaceDebugTint = Color.white;
            VoxelRenderBridge.FarFieldEnabled = false;
            VoxelRenderBridge.SolidBuildBudgetMs = 10.0;
            VoxelRenderBridge.WaterBuildBudgetMs = 0.0;

            VoxelPresentationCatalogue.MaterialAlbedo[1] = new Vector4(0.55f, 0.52f, 0.47f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[2] = new Vector4(0.61f, 0.58f, 0.52f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[3] = new Vector4(0.42f, 0.41f, 0.39f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[4] = new Vector4(0.24f, 0.14f, 0.075f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[5] = new Vector4(0.22f, 0.38f, 0.13f, 1f);
            VoxelPresentationCatalogue.MaterialAlbedo[6] = new Vector4(0.30f, 0.58f, 0.70f, 1f);
            VoxelPresentationCatalogue.CoatingTint[Coatings.Moss] = mossEnabled
                ? new Vector4(0.16f, 0.40f, 0.11f, mossStrength)
                : Vector4.zero;
        }
    }
}