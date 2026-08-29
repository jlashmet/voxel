using System;
using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        // The issue's marked volume is centred near (-37.9 m, -67.4 m, -37.8 m). The cave
        // terminal is fixed in X/Z there while Y is derived from the actual terrain at its mouth.
        private const int UndergroundCavernTargetX = -378;
        private const int UndergroundCavernTargetZ = -378;
        private const int UndergroundCaveClearance = 12;
        private const int UndergroundCaveSegmentLength = 56;
        private const int UndergroundCaveSegments = 58;
        private const int UndergroundCaveDescentSegments = 52;
        private const int UndergroundCaveDescentPerSegment = 18;
        private const ulong UndergroundCaveSeed = 0x564F584341564552ul; // VOXCAVER

        private bool _undergroundCavernRuinsAuthored;

        public bool HasUndergroundCavernRuins => _undergroundCavernRuinsAuthored;
        public int UndergroundCavernTraversalDistance { get; private set; }
        public int UndergroundCavernStatueCount { get; private set; }
        public int UndergroundCavernStalactiteCount { get; private set; }
        public int UndergroundCavernGeologicalCategoryCount { get; private set; }
        public int UndergroundCavernLocalLightCount { get; private set; }
        public long UndergroundCavernVoxelsWritten { get; private set; }
        public float3 UndergroundCavernCentreMetres { get; private set; }
        public float3 UndergroundCavernEntranceMetres { get; private set; }

        /// <summary>
        /// Authors the main showcase's deep cavern through the same generic structure session used
        /// by production WorldBuilder content. It is idempotent for one ShowcaseWorld lifetime.
        /// </summary>
        public void GenerateUndergroundCavernRuinsBlocking()
        {
            if (_undergroundCavernRuinsAuthored) return;

            BuildUndergroundCaveDefinition(
                out CaveGenerationRequest caveRequest,
                out CaveConfig caveConfig,
                out CaveMaterialPalette cavePalette);
            UndergroundCavernRuinConfig ruinConfig = UndergroundCavernRuinConfig.DeepAncientRuin;

            PreloadUndergroundCavernRegions(in caveRequest, in caveConfig, in ruinConfig);

            var authoring = StructuresComposition.CreateAuthoringSession(
                ReadStorage,
                MutationStorage,
                _palette,
                writeBudget: 55_000_000);
            UndergroundCavernRuinResult result = UndergroundCavernRuinAuthoring.Author(
                authoring,
                in caveRequest,
                in caveConfig,
                in cavePalette,
                in ruinConfig);
            if (authoring.BudgetExceeded || !result.IsWellFormed)
                throw new InvalidOperationException(
                    "Underground cavern/ruin authoring exceeded its budget or produced incomplete semantic output.");

            _undergroundCavernRuinsAuthored = true;
            UndergroundCavernTraversalDistance = result.Destination.TraversalDistance;
            UndergroundCavernStatueCount = result.StatueCount;
            UndergroundCavernStalactiteCount = result.StalactiteCount;
            UndergroundCavernGeologicalCategoryCount = result.GeologicalCategoryCount;
            UndergroundCavernLocalLightCount = result.LocalLights.Length;
            UndergroundCavernVoxelsWritten = result.VoxelsWritten;
            UndergroundCavernCentreMetres =
                ((float3)(result.CavernBounds.Min + result.CavernBounds.MaxExclusive) * 0.5f) * VoxelSize;
            UndergroundCavernEntranceMetres = (float3)caveRequest.EntranceWorldPosition * VoxelSize;

            AppendUndergroundCavernLights(result.LocalLights);

            // Runtime bake restoration authors this feature after installing snapshots, so publish
            // every deep region now. During offline baking there are no live render consumers, but
            // the same notifications are harmless and keep the path identical.
            PublishUndergroundCavernRegions(in caveRequest, in caveConfig, in ruinConfig);
        }

        private void BuildUndergroundCaveDefinition(
            out CaveGenerationRequest request,
            out CaveConfig config,
            out CaveMaterialPalette palette)
        {
            int entranceX = UndergroundCavernTargetX
                - UndergroundCaveClearance
                - UndergroundCaveSegmentLength * UndergroundCaveSegments;
            int entranceY = TerrainQuery.HeightAt(entranceX, UndergroundCavernTargetZ, Seed) + 1;
            int3 entrance = new int3(entranceX, entranceY, UndergroundCavernTargetZ);

            request = CaveGenerationRequest.Standalone(
                UndergroundCaveSeed,
                Seed,
                entrance,
                Facing.East,
                28,
                32,
                UndergroundCaveClearance);

            config = CaveConfig.Default;
            config.TunnelWidth = 28;
            config.TunnelHeight = 32;
            config.SegmentLength = UndergroundCaveSegmentLength;
            config.MainSegmentCount = UndergroundCaveSegments;
            config.TurnChancePercent = 0;
            config.VerticalChancePercent = 0;
            config.MaxVerticalStepPerSegment = 0;
            config.SurfaceDescentSegments = UndergroundCaveDescentSegments;
            config.SurfaceDescentPerSegment = UndergroundCaveDescentPerSegment;
            config.MinimumSurfaceCover = 18;
            config.BranchChancePercent = 0;
            config.MaxBranches = 0;
            config.MaxBranchDepth = 0;
            config.ChamberChancePercent = 12;
            config.MinChamberRadius = 18;
            config.MaxChamberRadius = 30;
            config.MinChamberHeight = 34;
            config.MaxChamberHeight = 48;
            config.FloorRoughness = 2;
            config.CeilingRoughness = 4;
            config.WallRoughness = 3;
            config.BoundsHalfExtents = new int3(3400, 1120, 320);
            config.MinVerticalOffset = -1000;
            config.MaxVerticalOffset = 24;

            palette = new CaveMaterialPalette
            {
                Opening = GameMaterialIds.Empty,
                Rock = GameMaterialIds.DarkStone,
                Accent = GameMaterialIds.Crystal,
                Decoration = GameMaterialIds.Moss,
                Water = GameMaterialIds.Water,
            };
        }

        private void PreloadUndergroundCavernRegions(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig ruin)
        {
            var regions = CollectUndergroundCavernRegions(in request, in cave, in ruin);
            foreach (int3 region in regions)
                GenerateRegionBlocking(region);
        }

        private void PublishUndergroundCavernRegions(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig ruin)
        {
            var regions = CollectUndergroundCavernRegions(in request, in cave, in ruin);
            foreach (int3 region in regions)
                _changes.PublishRegion(region, VoxelEngine.Storage.Api.VoxelChangeKind.All);
        }

        private HashSet<int3> CollectUndergroundCavernRegions(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig ruin)
        {
            var regions = new HashSet<int3>();
            int3 direction = new int3(1, 0, 0);
            int3 current = request.EntranceWorldPosition + direction * request.Entrance.ClearanceLength;
            AddRegionNeighbourhood(regions, current, 1);

            for (int segment = 0; segment < cave.MainSegmentCount; segment++)
            {
                int drop = segment < cave.SurfaceDescentSegments ? cave.SurfaceDescentPerSegment : 0;
                current = new int3(
                    current.x + cave.SegmentLength,
                    math.max(current.y - drop, request.Origin.y + cave.MinVerticalOffset),
                    current.z);
                AddRegionNeighbourhood(regions, current, 1);
            }

            int3 cavernCentre = current + direction * 34;
            int padding = ruin.CavernRadius + 24;
            AddRegionBounds(
                regions,
                cavernCentre - new int3(padding, 16, padding),
                cavernCentre + new int3(padding + ruin.RuinForwardOffset + ruin.RuinDepth,
                    ruin.CavernHeight + 24,
                    padding));
            return regions;
        }

        private static void AddRegionNeighbourhood(HashSet<int3> regions, int3 voxel, int radius)
        {
            int3 centre = RegionAt((float3)voxel * VoxelSize);
            for (int y = -radius; y <= radius; y++)
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
                regions.Add(centre + new int3(x, y, z));
        }

        private static void AddRegionBounds(HashSet<int3> regions, int3 minVoxel, int3 maxVoxel)
        {
            int3 first = RegionAt((float3)minVoxel * VoxelSize);
            int3 last = RegionAt((float3)maxVoxel * VoxelSize);
            for (int y = first.y; y <= last.y; y++)
            for (int z = first.z; z <= last.z; z++)
            for (int x = first.x; x <= last.x; x++)
                regions.Add(new int3(x, y, z));
        }

        private void AppendUndergroundCavernLights(MineCaveLightRequest[] lights)
        {
            int oldCount = CastlePresentationLights.Length;
            var positions = new Vector4[oldCount + lights.Length];
            var colours = new Vector4[oldCount + lights.Length];
            Array.Copy(CastlePresentationLights, positions, oldCount);
            Array.Copy(CastlePresentationLightColours, colours, oldCount);

            var warmTorch = new Vector4(1.00f, 0.25f, 0.045f, 1.35f);
            for (int i = 0; i < lights.Length; i++)
            {
                float3 p = lights[i].PositionVoxels * VoxelSize;
                positions[oldCount + i] = new Vector4(p.x, p.y, p.z, 6.5f);
                colours[oldCount + i] = warmTorch;
            }
            CastlePresentationLights = positions;
            CastlePresentationLightColours = colours;
        }
    }
}
