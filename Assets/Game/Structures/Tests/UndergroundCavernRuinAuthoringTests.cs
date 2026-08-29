using System;
using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class UndergroundCavernRuinAuthoringTests
    {
        [Test]
        public void DeepAncientRuin_UsesReachableTraversalAndSharedGeologyLights()
        {
            var authoring = new CountingAuthoringSession();
            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x564F584341564552ul,
                0x5EED1234u,
                new int3(-3638, 260, -378),
                Facing.East,
                28,
                32,
                12);
            CaveConfig cave = ProductionLikeCave();

            var palette = CavePalette();
            UndergroundCavernRuinConfig ruin = UndergroundCavernRuinConfig.DeepAncientRuin;
            UndergroundCavernTraversalProfile traversal = UndergroundCavernTraversalProfile.LongDescent;

            UndergroundCavernRuinResult result = UndergroundCavernRuinAuthoring.Author(
                authoring, in request, in cave, in palette, in ruin, in traversal);
            UndergroundCavernTraversalEnhancementResult route =
                UndergroundCavernTraversalEnhancement.Author(
                    authoring, in request, in cave, in palette, in traversal);

            Assert.That(result.IsWellFormed, Is.True);
            Assert.That(result.Destination.TraversalDistance,
                Is.GreaterThanOrEqualTo(ruin.MinimumDestinationTraversal));
            Assert.That(result.Destination.Flags & CaveTraversalFlags.ReachableFromEntrance,
                Is.Not.EqualTo(CaveTraversalFlags.None));
            Assert.That(result.CavernBounds.Size.x, Is.GreaterThan(cave.TunnelWidth * 8));
            Assert.That(result.CavernBounds.Size.y, Is.GreaterThan(cave.TunnelHeight * 4));
            Assert.That(result.StatueCount, Is.EqualTo(2));
            Assert.That(result.StalactiteCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.GeologicalCategoryCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(result.LocalLights.Length, Is.InRange(1, 8));
            Assert.That(route.IsWellFormed, Is.True);
            Assert.That(route.DoglegCount, Is.EqualTo(traversal.BendPositionsPermille.Length));
            Assert.That(route.RouteLights.Length, Is.EqualTo(traversal.RouteLightPositionsPermille.Length));
            Assert.That(authoring.EmptyGeometryCalls, Is.GreaterThan(3));
            Assert.That(authoring.DarkStoneGeometryCalls, Is.GreaterThan(3));
            Assert.That(authoring.MasonryGeometryCalls, Is.GreaterThan(5));
            Assert.That(result.VoxelsWritten + route.VoxelsWritten, Is.LessThanOrEqualTo(55_000_000));
        }

        [Test]
        public void PortableProfile_AuthorsSecondFacingLengthSeedWithRuntimeRegionsAndLights()
        {
            var authoring = new CountingAuthoringSession();
            CaveGenerationRequest request = CaveGenerationRequest.Standalone(
                0x504F525443415645ul, // PORTCAVE
                0xA17E4421u,
                new int3(920, 240, 1460),
                Facing.South,
                28,
                32,
                14);
            CaveConfig cave = ProductionLikeCave();
            cave.SegmentLength = 48;
            cave.MainSegmentCount = 54;
            cave.SurfaceDescentSegments = 44;
            cave.SurfaceDescentPerSegment = 12;
            cave.BoundsHalfExtents = new int3(420, 860, 3000);
            cave.MinVerticalOffset = -760;

            var palette = CavePalette();
            UndergroundCavernRuinConfig ruin = UndergroundCavernRuinConfig.DeepAncientRuin;
            ruin.MinimumDestinationTraversal = 1800;
            ruin.CavernRadius = 148;
            ruin.CavernHeight = 172;
            ruin.RuinForwardOffset = 70;
            ruin.LanternInstancesPerKind = 2;

            var traversal = new UndergroundCavernTraversalProfile
            {
                BendPositionsPermille = new[] { 240, 515, 780 },
                RouteLightPositionsPermille = new[] { 120, 370, 690, 910 },
                BendForwardOffsets = new[] { -22, -12, -4, 6, 16, 24 },
                BendSideOffsets = new[] { 0, 8, 18, 26, 14, 2 },
                BendSideReach = 26,
                BendRadius = 13,
            };

            UndergroundCavernRuinResult result = UndergroundCavernRuinAuthoring.Author(
                authoring, in request, in cave, in palette, in ruin, in traversal);
            UndergroundCavernTraversalEnhancementResult route =
                UndergroundCavernTraversalEnhancement.Author(
                    authoring, in request, in cave, in palette, in traversal);

            Assert.That(result.IsWellFormed, Is.True);
            Assert.That(result.Destination.Flags & CaveTraversalFlags.ReachableFromEntrance,
                Is.Not.EqualTo(CaveTraversalFlags.None));
            Assert.That(result.Destination.TraversalDistance,
                Is.GreaterThanOrEqualTo(ruin.MinimumDestinationTraversal));
            Assert.That(route.IsWellFormed, Is.True);
            Assert.That(route.DoglegCount, Is.EqualTo(3));
            Assert.That(route.DirectionChangeCount, Is.EqualTo(6));
            Assert.That(route.RouteLights.Length, Is.EqualTo(4));
            Assert.That(HasLateralRouteExcursion(route.TraversalWaypoints, request.Entrance.Facing), Is.True,
                "The portable route should include real lateral bends, not just a cardinal terminal ray.");

            MineCaveLightRequest[] allLights = Combine(route.RouteLights, result.LocalLights);
            UndergroundCavernLocalLight[] localLights = UndergroundCavernRuntimeSupport.BuildLocalLights(
                allLights,
                voxelSizeMetres: 0.1f,
                radiusMetres: 6.0f,
                colourAndIntensity: new float4(1.0f, 0.28f, 0.07f, 1.2f));
            Assert.That(localLights.Length, Is.EqualTo(allLights.Length));
            Assert.That(localLights.Length, Is.GreaterThanOrEqualTo(5));
            for (int i = 0; i < localLights.Length; i++)
                Assert.That(localLights[i].IsWellFormed, Is.True, $"Local light {i} should be renderer-neutral and valid.");

            var runtime = new RecordingRegionRuntime();
            int prepared = UndergroundCavernRuntimeSupport.PrepareAffectedRegions(
                runtime, in request, in cave, in ruin, in traversal, regionVoxelEdge: 320);
            int published = UndergroundCavernRuntimeSupport.PublishAffectedRegions(
                runtime, in request, in cave, in ruin, in traversal, regionVoxelEdge: 320);
            Assert.That(prepared, Is.GreaterThan(6));
            Assert.That(published, Is.EqualTo(prepared));
            CollectionAssert.AreEquivalent(runtime.Prepared, runtime.Published,
                "The same reusable affected-region envelope must drive preload and publication.");

            int[] resolvedBends = traversal.ResolveBendSegments(in cave);
            int[] resolvedLights = traversal.ResolveRouteLightSegments(in cave);
            Assert.That(resolvedBends, Is.Not.EqualTo(UndergroundCavernTraversalProfile.LongDescent.ResolveBendSegments(in cave)));
            Assert.That(resolvedLights.Length, Is.EqualTo(4));
            Assert.That(authoring.TotalVoxelsWritten, Is.GreaterThan(0));
        }

        private static CaveConfig ProductionLikeCave()
        {
            CaveConfig cave = CaveConfig.Default;
            cave.TunnelWidth = 28;
            cave.TunnelHeight = 32;
            cave.SegmentLength = 56;
            cave.MainSegmentCount = 58;
            cave.TurnChancePercent = 0;
            cave.VerticalChancePercent = 0;
            cave.MaxVerticalStepPerSegment = 0;
            cave.SurfaceDescentSegments = 52;
            cave.SurfaceDescentPerSegment = 18;
            cave.MinimumSurfaceCover = 18;
            cave.BranchChancePercent = 0;
            cave.MaxBranches = 0;
            cave.MaxBranchDepth = 0;
            cave.ChamberChancePercent = 12;
            cave.MinChamberRadius = 18;
            cave.MaxChamberRadius = 30;
            cave.MinChamberHeight = 34;
            cave.MaxChamberHeight = 48;
            cave.FloorRoughness = 2;
            cave.CeilingRoughness = 4;
            cave.WallRoughness = 3;
            cave.BoundsHalfExtents = new int3(3400, 1120, 320);
            cave.MinVerticalOffset = -1000;
            cave.MaxVerticalOffset = 24;
            return cave;
        }

        private static CaveMaterialPalette CavePalette() => new CaveMaterialPalette
        {
            Opening = GameMaterialIds.Empty,
            Rock = GameMaterialIds.DarkStone,
            Accent = GameMaterialIds.Crystal,
            Decoration = GameMaterialIds.Moss,
            Water = GameMaterialIds.Water,
        };

        private static MineCaveLightRequest[] Combine(
            MineCaveLightRequest[] route,
            MineCaveLightRequest[] cavern)
        {
            int routeCount = route?.Length ?? 0;
            int cavernCount = cavern?.Length ?? 0;
            var combined = new MineCaveLightRequest[routeCount + cavernCount];
            if (routeCount > 0) Array.Copy(route, combined, routeCount);
            if (cavernCount > 0) Array.Copy(cavern, 0, combined, routeCount, cavernCount);
            return combined;
        }

        private static bool HasLateralRouteExcursion(int3[] waypoints, Facing facing)
        {
            if (waypoints == null || waypoints.Length == 0) return false;
            int baseline = facing == Facing.East || facing == Facing.West
                ? waypoints[0].z
                : waypoints[0].x;
            for (int i = 1; i < waypoints.Length; i++)
            {
                int lateral = facing == Facing.East || facing == Facing.West
                    ? waypoints[i].z
                    : waypoints[i].x;
                if (math.abs(lateral - baseline) >= 8) return true;
            }
            return false;
        }

        private sealed class RecordingRegionRuntime : IUndergroundCavernRegionRuntime
        {
            public readonly HashSet<int3> Prepared = new();
            public readonly HashSet<int3> Published = new();

            public void EnsureRegionResident(int3 region) => Prepared.Add(region);
            public void PublishRegion(int3 region) => Published.Add(region);
        }

        private sealed class CountingAuthoringSession : IStructureAuthoringSession
        {
            private long _writes;

            public int EmptyGeometryCalls { get; private set; }
            public int DarkStoneGeometryCalls { get; private set; }
            public int MasonryGeometryCalls { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => _writes;

            public byte Get(int x, int y, int z) => GameMaterialIds.Empty;
            public byte GetCoating(int x, int y, int z) => Coatings.None;
            public bool IsSolid(int x, int y, int z) => false;

            public void Set(int x, int y, int z, byte material) => Add(material, 1);

            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) =>
                Add(material, 1);

            public void Coat(int x, int y, int z, byte coating) { }

            public void FillBulk(int3 min, int3 size, byte material) =>
                Add(material, Volume(size));

            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) =>
                Add(material, math.max(0, maxYExclusive - minY));

            public void Box(int3 min, int3 size, byte material) =>
                Add(material, Volume(size));

            public void HollowBox(int3 min, int3 size, int thickness, byte material,
                bool floor, bool ceiling)
            {
                long outer = Volume(size);
                int3 inner = math.max(int3.zero, size - new int3(thickness * 2, thickness * 2, thickness * 2));
                Add(material, math.max(1L, outer - Volume(inner)));
            }

            public void Cylinder(int cx, int baseY, int cz, int radius, int height,
                byte material, int innerRadius = 0)
            {
                long annulus = math.max(1L,
                    (long)(radius * radius - innerRadius * innerRadius) * 314L / 100L);
                Add(material, annulus * math.max(0, height));
            }

            public void Disc(int cx, int y, int cz, int radius, byte material) =>
                Add(material, math.max(1L, (long)radius * radius * 314L / 100L));

            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Add(material, math.max(1L, (long)radius * radius * math.max(0, height) * 105L / 100L));

            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) =>
                Cone(cx, ceilingY - height, cz, radius, height, material);

            public void Gable(int3 min, int3 size, bool alongX, byte material) =>
                Add(material, Volume(size) / 2);

            public void Crenellate(int3 start, int3 step, int count, int width, int height,
                int merlon, int gap, byte material) =>
                Add(material, (long)math.max(0, count) * math.max(0, width) * math.max(0, height));

            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) =>
                Add(material, (long)radius * math.max(0, height) * 12L);

            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) =>
                Add(material, (long)math.max(0, width) * math.max(0, height) * math.max(0, depth));

            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) =>
                Add(material, (long)math.max(0, width) * math.max(0, steps) * math.max(0, rise) * math.max(0, run));

            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) =>
                Add(material, (long)math.max(0, height) * 9L);

            public void Carve(int3 min, int3 size) => Add(GameMaterialIds.Empty, Volume(size));
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }

            private void Add(byte material, long count)
            {
                _writes += math.max(0L, count);
                if (material == GameMaterialIds.Empty) EmptyGeometryCalls++;
                if (material == GameMaterialIds.DarkStone) DarkStoneGeometryCalls++;
                if (material == GameMaterialIds.MasonrySmall ||
                    material == GameMaterialIds.MasonryMedium ||
                    material == GameMaterialIds.MasonryLarge)
                    MasonryGeometryCalls++;
            }

            private static long Volume(int3 size) =>
                (long)math.max(0, size.x) * math.max(0, size.y) * math.max(0, size.z);
        }
    }
}
