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
        private static void BuildWallDesignScene(NativeList<Primitive> output, ProfileBlockStore profiles)
        {
            int order = 0;
            // Ground.
            output.Add(RoundedBox(new int3(-145, 0, -50), new int3(310, 8, 180), 6,
                                  2, SurfaceStyles.Rounded, ref order));
            output.Add(RoundedBox(new int3(-132, 8, -36), new int3(284, 7, 152), 5,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));

            // Long articulated wall, three offset runs. Gaps deliberately produce depth.
            BuildWallRun(output, profiles, new int3(-125, 14, 24), 86, 54, false, ref order);
            BuildWallRun(output, profiles, new int3(-31, 17, 36), 76, 61, true, ref order);
            BuildWallRun(output, profiles, new int3(56, 13, 18), 83, 48, false, ref order);

            // A round-ish watchtower breaks the silhouette on the right.
            for (int tier = 0; tier < 5; tier++)
            {
                int inset = tier * 2;
                output.Add(RoundedBox(new int3(111 + inset, 14 + tier * 18, 52 + inset),
                                      new int3(48 - inset * 2, 20, 48 - inset * 2), 7,
                                      tier < 3 ? (byte)1 : (byte)2,
                                      tier < 3 ? SurfaceStyles.MasonryJoint : SurfaceStyles.Rounded,
                                      ref order));
            }
            AddBattlements(output, new int3(110, 104, 51), new int3(50, 9, 50), 1, ref order);

            // Garden terrace in front/right.
            output.Add(RoundedBox(new int3(38, 13, -25), new int3(92, 6, 28), 5,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));
            output.Add(RoundedBox(new int3(55, 18, -15), new int3(55, 4, 17), 4,
                                  5, SurfaceStyles.Smooth, ref order, coating: Coatings.Moss));

            // Broken cliff shelves, left side.
            for (int i = 0; i < 6; i++)
            {
                output.Add(RoundedBox(new int3(-151 + i * 3, 4 + i * 3, -42 + i * 25),
                                      new int3(36, 10, 21), 4,
                                      2, SurfaceStyles.Rounded, ref order));
            }
        }

        private static void BuildWallRun(NativeList<Primitive> output, ProfileBlockStore profiles,
                                         int3 min, int length, int height, bool largeGate,
                                         ref int order)
        {
            byte stone = 1;
            byte trim = 2;
            output.Add(RoundedBox(min, new int3(length, height, 15), 3,
                                  stone, SurfaceStyles.MasonryJoint, ref order));

            // Base batter and cap.
            output.Add(RoundedBox(min + new int3(-4, 0, -4), new int3(length + 8, 11, 23), 3,
                                  trim, SurfaceStyles.Rounded, ref order));
            output.Add(RoundedBox(min + new int3(-2, height - 7, -2),
                                  new int3(length + 4, 8, 19), 2,
                                  trim, SurfaceStyles.Rounded, ref order));

            // Vertical buttresses and recessed panels.
            for (int x = 10; x < length - 7; x += 22)
            {
                output.Add(RoundedBox(min + new int3(x, 4, -6), new int3(7, height - 1, 13), 2,
                                      trim, SurfaceStyles.Rounded, ref order));
            }

            // A gate or postern — use the same retained-profile arch machinery as the hero.
            int span = largeGate ? 24 : 16;
            int ring = largeGate ? 6 : 5;
            int pier = largeGate ? 28 : 22;
            int xOffset = length / 2 - span / 2 - ring;
            var arch = new ArchFeatureDefinition
            {
                ClearSpan = span,
                PierHeight = pier,
                RingThickness = ring,
                Depth = 7,
                VoussoirCount = largeGate ? 11 : 9,
                JointRecessDepth = 1,
                StoneMaterial = stone,
                PierStyle = SurfaceStyles.MasonryJoint,
                RingStyle = SurfaceStyles.MasonryJoint,
            };
            arch.Emit(min + new int3(xOffset, 0, -9), output, profiles);
            output.Add(Box(min + new int3(length / 2 - span / 2, 0, -2),
                           new int3(span, pier + 2, 19), stone,
                           SurfaceStyles.MasonryJoint, ref order, PrimitiveMode.Carve));

            AddBattlements(output, min + new int3(-2, height, -2),
                           new int3(length + 4, 9, 19), stone, ref order);
        }

        private static WorldState BuildWallWorld()
        {
            var world = new WorldState
            {
                Table = new RegionTable(8, Allocator.Persistent),
                Pool = new BrickPool(80_000, Allocator.Persistent),
                Palette = default,
                Surfaces = SurfaceCatalogue.CreateBuiltIns(),
                Coatings = CoatingCatalogue.CreateBuiltIns(),
                Profiles = new ProfileBlockStore(),
                Changes = new VoxelChangeJournal(),
            };
            ConfigurePalette(ref world.Palette);
            var primitives = new NativeList<Primitive>(384, Allocator.Temp);
            try
            {
                BuildWallDesignScene(primitives, world.Profiles);
                RasterResult result = PrimitiveRasteriser.Rasterise(
                    primitives.AsArray(), new int3(-170, -10, -80), new int3(190, 160, 170),
                    ref world.Table, ref world.Pool);
                if (result.BudgetExceeded)
                    throw new InvalidOperationException("wall-design primitive budget exceeded");
            }
            finally { primitives.Dispose(); }
            using NativeArray<int3> regions = world.Table.GetResidentCoords(Allocator.Temp);
            for (int i = 0; i < regions.Length; i++) world.Changes.PublishRegion(regions[i]);
            return world;
        }

        private static void CaptureWallDesign(string outputDirectory)
        {
            using WorldState world = BuildWallWorld();
            var captures = new List<string>
            {
                Capture(outputDirectory, world, "wall-oblique", true, 0.72f, camera =>
                {
                    camera.transform.position = new Vector3(-17.5f, 8.5f, -20.0f);
                    camera.transform.LookAt(new Vector3(0.8f, 5.8f, 5.0f));
                    camera.fieldOfView = 38f;
                }),
                Capture(outputDirectory, world, "wall-gate", true, 0.72f, camera =>
                {
                    camera.transform.position = new Vector3(-6.0f, 4.8f, -16.5f);
                    camera.transform.LookAt(new Vector3(0.5f, 4.2f, 3.7f));
                    camera.fieldOfView = 35f;
                }),
                Capture(outputDirectory, world, "wall-long", true, 0.72f, camera =>
                {
                    camera.transform.position = new Vector3(21.0f, 6.6f, -13.5f);
                    camera.transform.LookAt(new Vector3(4.0f, 5.5f, 5.2f));
                    camera.fieldOfView = 43f;
                }),
            };
            File.WriteAllLines(Path.Combine(outputDirectory, "wall-manifest.txt"), captures);
            Debug.Log($"WALL_DESIGN_COMPLETE {outputDirectory}");
        }
    }
}