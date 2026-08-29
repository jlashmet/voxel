using System;
using Game.Materials.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Storage.Api;
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
        private const int UndergroundCavernMaximumLocalLights = 8;
        private const string UndergroundCavernLightContributorKey = "voxel-showcase-underground-cavern";

        private bool _undergroundCavernRuinsAuthored;

        public bool HasUndergroundCavernRuins => _undergroundCavernRuinsAuthored;
        public int UndergroundCavernTraversalDistance { get; private set; }
        public int UndergroundCavernStatueCount { get; private set; }
        public int UndergroundCavernStalactiteCount { get; private set; }
        public int UndergroundCavernGeologicalCategoryCount { get; private set; }
        public int UndergroundCavernLocalLightCount { get; private set; }
        public int UndergroundCavernRouteLightCount { get; private set; }
        public int UndergroundCavernDirectionChangeCount { get; private set; }
        public int UndergroundCavernMouthOpeningCount { get; private set; }
        public int UndergroundCavernPreloadedRegionCount { get; private set; }
        public int UndergroundCavernIrregularLobeCount { get; private set; }
        public int UndergroundCavernArchitectureDetailCount { get; private set; }
        public int UndergroundCavernStatueDetailCount { get; private set; }
        public int UndergroundCavernAdditionalFormationCount { get; private set; }
        public long UndergroundCavernVisualFinishVoxelsWritten { get; private set; }
        public long UndergroundCavernVoxelsWritten { get; private set; }
        public float3 UndergroundCavernCentreMetres { get; private set; }
        public float3 UndergroundCavernEntranceMetres { get; private set; }
        public float3[] UndergroundCavernTraversalWaypointsMetres { get; private set; } = Array.Empty<float3>();

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
            UndergroundCavernTraversalProfile traversalProfile =
                UndergroundCavernTraversalProfile.LongDescent;
            // Preserve the existing eight-light feature ceiling by spending the profile's sparse
            // route fixtures on the long descent and at most two inside the destination cavern.
            ruinConfig.LanternInstancesPerKind = 2;

            var regionRuntime = new ShowcaseUndergroundCavernRegionRuntime(this);
            UndergroundCavernPreloadedRegionCount =
                UndergroundCavernRuntimeSupport.PrepareAffectedRegions(
                    regionRuntime,
                    in caveRequest,
                    in caveConfig,
                    in ruinConfig,
                    in traversalProfile,
                    RegionVoxelEdge);

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
                in ruinConfig,
                in traversalProfile);
            if (!result.IsWellFormed)
                throw new InvalidOperationException(
                    "Underground cavern/ruin authoring produced incomplete semantic output.");

            UndergroundCavernTraversalEnhancementResult traversal =
                UndergroundCavernTraversalEnhancement.Author(
                    authoring,
                    in caveRequest,
                    in caveConfig,
                    in cavePalette,
                    in traversalProfile);

            UndergroundCavernVisualFinishResult visualFinish =
                UndergroundCavernVisualFinish.Author(
                    authoring,
                    in result,
                    result.Destination.ExitFacing,
                    in cavePalette,
                    caveRequest.TerrainSeed);
            UndergroundCavernCirculationProtection.Reassert(
                authoring,
                in result.RuinBounds,
                result.Destination.ExitFacing);

            MineCaveLightRequest[] allLights =
                CombineUndergroundCavernLights(traversal.RouteLights, result.LocalLights);
            if (authoring.BudgetExceeded || !traversal.IsWellFormed || !visualFinish.IsWellFormed ||
                allLights.Length > UndergroundCavernMaximumLocalLights)
                throw new InvalidOperationException(
                    "Underground cavern traversal/visual finish exceeded its budget, semantic contract, or local-light cap.");

            _undergroundCavernRuinsAuthored = true;
            UndergroundCavernTraversalDistance = result.Destination.TraversalDistance;
            UndergroundCavernStatueCount = result.StatueCount;
            UndergroundCavernStalactiteCount = result.StalactiteCount;
            UndergroundCavernGeologicalCategoryCount = result.GeologicalCategoryCount;
            UndergroundCavernLocalLightCount = allLights.Length;
            UndergroundCavernRouteLightCount = traversal.RouteLights.Length;
            UndergroundCavernDirectionChangeCount = traversal.DirectionChangeCount;
            UndergroundCavernMouthOpeningCount = traversal.MouthOpeningCount;
            UndergroundCavernIrregularLobeCount = visualFinish.IrregularLobeCount;
            UndergroundCavernArchitectureDetailCount = visualFinish.ArchitecturalDetailCount;
            UndergroundCavernStatueDetailCount = visualFinish.StatueDetailCount;
            UndergroundCavernAdditionalFormationCount = visualFinish.AdditionalFormationCount;
            UndergroundCavernVisualFinishVoxelsWritten = visualFinish.VoxelsWritten;
            UndergroundCavernVoxelsWritten = authoring.TotalVoxelsWritten;
            UndergroundCavernCentreMetres =
                ((float3)(result.CavernBounds.Min + result.CavernBounds.MaxExclusive) * 0.5f) * VoxelSize;
            UndergroundCavernEntranceMetres = (float3)caveRequest.EntranceWorldPosition * VoxelSize;
            UndergroundCavernTraversalWaypointsMetres =
                BuildUndergroundCavernTraversalMetres(traversal.TraversalWaypoints, in result);

            RegisterUndergroundCavernLights(allLights);

            // Runtime bake restoration authors this feature after installing snapshots, so publish
            // every deep region now. During offline baking there are no live render consumers, but
            // the same notifications are harmless and keep the path identical.
            UndergroundCavernRuntimeSupport.PublishAffectedRegions(
                regionRuntime,
                in caveRequest,
                in caveConfig,
                in ruinConfig,
                in traversalProfile,
                RegionVoxelEdge);
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

        private static float3[] BuildUndergroundCavernTraversalMetres(
            int3[] authoredWaypoints,
            in UndergroundCavernRuinResult result)
        {
            int authoredCount = authoredWaypoints?.Length ?? 0;
            var route = new float3[authoredCount + 2];
            for (int i = 0; i < authoredCount; i++)
                route[i] = (float3)authoredWaypoints[i] * VoxelSize;

            int3 cavernFloor = new int3(
                (result.CavernBounds.Min.x + result.CavernBounds.MaxExclusive.x) / 2,
                result.CavernBounds.Min.y,
                (result.CavernBounds.Min.z + result.CavernBounds.MaxExclusive.z) / 2);
            int3 ruinFloor = new int3(
                (result.RuinBounds.Min.x + result.RuinBounds.MaxExclusive.x) / 2,
                result.RuinBounds.Min.y,
                (result.RuinBounds.Min.z + result.RuinBounds.MaxExclusive.z) / 2);
            route[authoredCount] = (float3)cavernFloor * VoxelSize;
            route[authoredCount + 1] = (float3)ruinFloor * VoxelSize;
            return route;
        }

        private static MineCaveLightRequest[] CombineUndergroundCavernLights(
            MineCaveLightRequest[] route,
            MineCaveLightRequest[] cavern)
        {
            int routeCount = route?.Length ?? 0;
            int cavernCount = cavern?.Length ?? 0;
            var combined = new MineCaveLightRequest[routeCount + cavernCount];
            if (routeCount > 0) Array.Copy(route, 0, combined, 0, routeCount);
            if (cavernCount > 0) Array.Copy(cavern, 0, combined, routeCount, cavernCount);
            return combined;
        }

        private static void RegisterUndergroundCavernLights(MineCaveLightRequest[] requests)
        {
            UndergroundCavernLocalLight[] lights = UndergroundCavernRuntimeSupport.BuildLocalLights(
                requests,
                VoxelSize,
                radiusMetres: 5.2f,
                colourAndIntensity: new float4(1.00f, 0.20f, 0.035f, 2.15f));
            var positions = new Vector4[lights.Length];
            var colours = new Vector4[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                float3 p = lights[i].PositionMetres;
                positions[i] = new Vector4(p.x, p.y, p.z, lights[i].RadiusMetres);
                float4 c = lights[i].ColourAndIntensity;
                colours[i] = new Vector4(c.x, c.y, c.z, c.w);
            }
            LocalLightContributorRegistry.Set(
                UndergroundCavernLightContributorKey,
                positions,
                colours);
        }

        private sealed class ShowcaseUndergroundCavernRegionRuntime : IUndergroundCavernRegionRuntime
        {
            private readonly ShowcaseWorld _world;

            public ShowcaseUndergroundCavernRegionRuntime(ShowcaseWorld world)
            {
                _world = world ?? throw new ArgumentNullException(nameof(world));
            }

            public void EnsureRegionResident(int3 region) =>
                _world.GenerateRegionBlocking(region);

            public void PublishRegion(int3 region) =>
                _world._changes.PublishRegion(region, VoxelChangeKind.All);
        }
    }
}
