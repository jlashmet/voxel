using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Voxel;
using VoxelEngine.Terrain.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Showcase-owned policy for the Mountain Dragon landmark. WorldBuilder owns reusable mountain
    /// shape, climate and road mechanics; this composition selects one natural mountain, one climate
    /// and a high-level spiral ascent intent without owning path voxels or traversal ramps.
    /// </summary>
    public static class ShowcaseMountainDragonLayout
    {
        public const int OriginX = -1712;
        public const int OriginZ = -400;
        public const int FootprintEdge = 1200;
        public const int MountainRadius = 500;
        public const int MountainHeight = 240;
        public const int SummitRadius = 80;
        public const int PathWidth = 30;
        public const int PlaceholderSize = 60;
        public const string AscentRouteId = "showcase-mountain-dragon-ascent";

        // Keep the same one-and-a-half-turn authored ascent as the earlier 13-point layout, but
        // sample it at half the angular/radial step. The shared road resolver grades between
        // semantic controls while retaining the same road grade and cut/fill contracts.
        private const int SpiralControlCount = 25;
        private const int EntryRadiusDm = MountainRadius + 50;
        private const int SummitApproachRadiusDm = SummitRadius + 25;
        private const int SummitArrivalRadiusDm = PlaceholderSize / 2 + PathWidth;

        private static readonly int[] DirectionX =
        {
            1024, 946, 724, 392, 0, -392, -724, -946,
            -1024, -946, -724, -392, 0, 392, 724, 946,
        };

        private static readonly int[] DirectionZ =
        {
            0, 392, 724, 946, 1024, 946, 724, 392,
            0, -392, -724, -946, -1024, -946, -724, -392,
        };

        public static int CentreXdm => OriginX + FootprintEdge / 2;
        public static int CentreZdm => OriginZ + FootprintEdge / 2;
        public static int EntryXdm => CentreXdm;
        public static int EntryZdm => CentreZdm - EntryRadiusDm;

        public static MountainLandformSpec CreateLandform(uint seed)
        {
            int baseY = TerrainQuery.HeightAt(EntryXdm, EntryZdm, seed) + 1;
            return new MountainLandformSpec(
                originXdm: CentreXdm,
                originYdm: baseY,
                originZdm: CentreZdm,
                radiusXdm: MountainRadius,
                // A visibly elliptical massif gives the landmark a natural asymmetric silhouette
                // while keeping every analytic mass broad enough that the spiral road does not run
                // between narrow cone/ridge walls. The previous 465dm near-circle plus tall ridge and
                // roughness frusta produced repeated 4m-class trench faces in built-player evidence.
                radiusZdm: MountainRadius - 100,
                heightDm: MountainHeight,
                summitRadiusDm: SummitRadius,
                macroShape: MountainMacroShape.Massif,
                summitCharacter: MountainSummitCharacter.Broad,
                seed: seed ^ 0xA4D14A6Fu,
                // Keep showcase policy on broad overlapping mountain masses. The reusable landform
                // still supports ridges/roughness for other consumers, but their narrow full-height
                // frusta were the demonstrated source of this encounter's wall-like road views.
                ridgeCount: 0,
                ridgeStrengthPermille: 0,
                asymmetryXPermille: 90,
                asymmetryZPermille: -70,
                roughnessAmplitudeDm: 0,
                roughnessScaleDm: 72,
                erosionStrengthPermille: 720);
        }

        public static MountainLandformSurface CreateSurface(uint seed)
        {
            MountainLandformSpec spec = CreateLandform(seed);
            return new MountainLandformSurface(in spec);
        }

        public static MountainClimateProfile CreateClimateProfile() =>
            new MountainClimateProfile(
                // Keep most of the approach in ground cover and restrict bright snow to the crest.
                // The prior 31%/74.5% bands amplified the already-too-steep ridge masses into huge
                // gray/white faces directly beside the road.
                groundCoverCeilingPermille: 450,
                snowLinePermille: 850,
                steepRockSlopePermille: 1100);

        public static WorldRoadNetwork CreateAscentNetwork(
            uint seed,
            MountainLandformSurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            var terrain = new MountainLandformRoadTerrain(
                surface,
                new ShowcaseBaseRoadTerrain(seed));
            IReadOnlyList<WorldRoadPlanPoint> controls = CreateAscentControls(surface);
            var profile = new WorldRoadProfile(
                id: "showcase-mountain-trail",
                surfaceId: "road-surface",
                carriagewayWidthDm: PathWidth,
                transitionWidthDm: 24,
                maximumGradePermille: 280,
                maximumCutFillDm: 42,
                edgeVariationDm: 2,
                vegetationSuppressionPermille: 1000,
                traversalCostPermille: 950,
                crossingPolicy: WorldRoadCrossingPolicy.AllowPass);
            var intent = new WorldRoadIntent(
                AscentRouteId,
                "showcase-mountain-entry",
                "showcase-mountain-summit",
                seed ^ 0x58F0A7D3u,
                profile,
                "Mountain Dragon composition: semantic spiral ascent over authored mountain surface",
                controls);

            ResolvedWorldRoad resolved = WorldRoadResolver.Resolve(
                intent,
                terrain,
                sampleSpacingDm: 20,
                searchMarginCells: 4);
            if (!resolved.IsResolved)
            {
                throw new InvalidOperationException(
                    "Mountain Dragon ascent could not be resolved: "
                    + resolved.Status + " " + resolved.FailureReason);
            }

            return new WorldRoadNetwork(new[]
            {
                new WorldRoadNetworkRoute(
                    resolved,
                    WorldRoadSemanticClass.Pedestrian,
                    shoulderWidthDm: 5,
                    clearanceWidthDm: 10,
                    markingPolicy: WorldRoadMarkingPolicy.None,
                    crosswalkPolicy: WorldRoadCrosswalkPolicy.None),
            });
        }

        public static ResolvedWorldRoadPoint SummitApproach(WorldRoadNetwork network)
        {
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (!network.TryGetRoute(AscentRouteId, out WorldRoadNetworkRoute route))
                throw new InvalidOperationException("Mountain Dragon ascent route is missing from its road network.");
            return route.Road.Points[route.Road.Points.Count - 1];
        }

        private static IReadOnlyList<WorldRoadPlanPoint> CreateAscentControls(
            MountainLandformSurface surface)
        {
            MountainLandformMass summit = surface.GetMass(0);
            var controls = new List<WorldRoadPlanPoint>(SpiralControlCount + 2);
            for (int i = 0; i < SpiralControlCount; i++)
            {
                int radius = EntryRadiusDm
                    - (EntryRadiusDm - SummitApproachRadiusDm) * i / (SpiralControlCount - 1);
                // 22.5 degrees per control over 24 intervals = 540 degrees / 1.5 turns.
                int direction = (12 + i) & 15;
                controls.Add(new WorldRoadPlanPoint(
                    summit.CentreXdm + DirectionX[direction] * radius / 1024,
                    summit.CentreZdm + DirectionZ[direction] * radius / 1024));
            }

            // Continue the same angular progression onto the broad summit. The temporary dragon
            // marker is centred on the crest, so the authored route must finish beside that solid
            // footprint rather than through it. One path width beyond the marker half-size keeps
            // the arrival on the supported summit while leaving normal player clearance.
            int summitDirection = (12 + SpiralControlCount) & 15;
            controls.Add(new WorldRoadPlanPoint(
                summit.CentreXdm + DirectionX[summitDirection] * SummitRadius / 1024,
                summit.CentreZdm + DirectionZ[summitDirection] * SummitRadius / 1024));
            controls.Add(new WorldRoadPlanPoint(
                summit.CentreXdm + DirectionX[summitDirection] * SummitArrivalRadiusDm / 1024,
                summit.CentreZdm + DirectionZ[summitDirection] * SummitArrivalRadiusDm / 1024));
            return controls;
        }

        private sealed class ShowcaseBaseRoadTerrain : IWorldRoadTerrain
        {
            private readonly uint _seed;

            public ShowcaseBaseRoadTerrain(uint seed) => _seed = seed;

            public int HeightAtDm(int xdm, int zdm) => TerrainQuery.HeightAt(xdm, zdm, _seed);

            public WorldRoadTerrainFlags FlagsAtDm(int xdm, int zdm) => WorldRoadTerrainFlags.None;
        }
    }
}
