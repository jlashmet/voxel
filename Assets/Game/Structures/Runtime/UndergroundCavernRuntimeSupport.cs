using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace Game.Structures.Runtime
{
    /// <summary>Renderer-neutral local light emitted by reusable underground authoring.</summary>
    public readonly struct UndergroundCavernLocalLight
    {
        public readonly float3 PositionMetres;
        public readonly float RadiusMetres;
        public readonly float4 ColourAndIntensity;
        public readonly uint Variant;

        public UndergroundCavernLocalLight(
            float3 positionMetres,
            float radiusMetres,
            float4 colourAndIntensity,
            uint variant)
        {
            PositionMetres = positionMetres;
            RadiusMetres = radiusMetres;
            ColourAndIntensity = colourAndIntensity;
            Variant = variant;
        }

        public bool IsWellFormed =>
            RadiusMetres > 0f && ColourAndIntensity.w > 0f && math.all(math.isfinite(PositionMetres));
    }

    /// <summary>
    /// World-runtime capability used by cave features to prepare and republish only the regions
    /// their authoring envelope can touch. Implementations remain owned by the active world.
    /// </summary>
    public interface IUndergroundCavernRegionRuntime
    {
        void EnsureRegionResident(int3 region);
        void PublishRegion(int3 region);
    }

    /// <summary>
    /// Reusable runtime envelope and presentation adapters for authored underground destinations.
    /// This keeps region residency/publication and local-light transport out of showcase-specific
    /// castle state while leaving the owning world responsible for actual storage and rendering.
    /// </summary>
    public static class UndergroundCavernRuntimeSupport
    {
        public static int PrepareAffectedRegions(
            IUndergroundCavernRegionRuntime runtime,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig ruin,
            in UndergroundCavernTraversalProfile traversal,
            int regionVoxelEdge)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            int3[] regions = CollectAffectedRegions(
                in request, in cave, in ruin, in traversal, regionVoxelEdge);
            for (int i = 0; i < regions.Length; i++)
                runtime.EnsureRegionResident(regions[i]);
            return regions.Length;
        }

        public static int PublishAffectedRegions(
            IUndergroundCavernRegionRuntime runtime,
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig ruin,
            in UndergroundCavernTraversalProfile traversal,
            int regionVoxelEdge)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            int3[] regions = CollectAffectedRegions(
                in request, in cave, in ruin, in traversal, regionVoxelEdge);
            for (int i = 0; i < regions.Length; i++)
                runtime.PublishRegion(regions[i]);
            return regions.Length;
        }

        public static int3[] CollectAffectedRegions(
            in CaveGenerationRequest request,
            in CaveConfig cave,
            in UndergroundCavernRuinConfig ruin,
            in UndergroundCavernTraversalProfile traversal,
            int regionVoxelEdge)
        {
            if (!request.IsWellFormed || !cave.IsWellFormed || !ruin.IsWellFormed || !traversal.IsWellFormed)
                throw new ArgumentException("Affected-region collection requires valid cave, ruin, and traversal configuration.");
            if (regionVoxelEdge <= 0)
                throw new ArgumentOutOfRangeException(nameof(regionVoxelEdge));

            var regions = new HashSet<int3>();
            int3 direction = FacingVector(request.Entrance.Facing);
            int bendChamberRadius = math.max(
                24,
                traversal.BendRadius + 12 + math.max(0, traversal.BendPositionsPermille.Length - 1) * 4);
            int routeHalfWidth = math.max(
                cave.TunnelWidth / 2 + cave.WallRoughness + ruin.HostPadding,
                math.max(
                    cave.MaxChamberRadius + cave.WallRoughness + 4,
                    traversal.BendSideReach + traversal.BendRadius + 12));
            routeHalfWidth = math.max(routeHalfWidth, bendChamberRadius + cave.WallRoughness + 4);
            int floorPadding = cave.FloorRoughness + ruin.HostPadding + 8;
            int ceilingPadding = math.max(
                cave.TunnelHeight + cave.CeilingRoughness + ruin.HostPadding + 8,
                math.max(cave.MaxChamberHeight, 42 + traversal.BendPositionsPermille.Length * 5)
                    + cave.CeilingRoughness + 8);

            int3 current = request.EntranceWorldPosition + direction * request.Entrance.ClearanceLength;
            AddRegionBounds(
                regions,
                request.EntranceWorldPosition - new int3(routeHalfWidth, floorPadding, routeHalfWidth),
                request.EntranceWorldPosition + new int3(routeHalfWidth, ceilingPadding, routeHalfWidth),
                regionVoxelEdge);

            for (int segment = 0; segment < cave.MainSegmentCount; segment++)
            {
                int drop = segment < cave.SurfaceDescentSegments ? cave.SurfaceDescentPerSegment : 0;
                int targetY = math.max(current.y - drop, request.Origin.y + cave.MinVerticalOffset);
                int3 next = new int3(
                    current.x + direction.x * cave.SegmentLength,
                    targetY,
                    current.z + direction.z * cave.SegmentLength);
                AddRegionBounds(
                    regions,
                    new int3(
                        math.min(current.x, next.x) - routeHalfWidth,
                        math.min(current.y, next.y) - floorPadding,
                        math.min(current.z, next.z) - routeHalfWidth),
                    new int3(
                        math.max(current.x, next.x) + routeHalfWidth,
                        math.max(current.y, next.y) + ceilingPadding,
                        math.max(current.z, next.z) + routeHalfWidth),
                    regionVoxelEdge);
                current = next;
            }

            int3 cavernCentre = current + direction * 34;
            int padding = ruin.CavernRadius + 24;
            int3 ruinReach = direction * (ruin.RuinForwardOffset + ruin.RuinDepth);
            AddRegionBounds(
                regions,
                new int3(
                    math.min(cavernCentre.x - padding, cavernCentre.x + ruinReach.x - padding),
                    cavernCentre.y - 16,
                    math.min(cavernCentre.z - padding, cavernCentre.z + ruinReach.z - padding)),
                new int3(
                    math.max(cavernCentre.x + padding, cavernCentre.x + ruinReach.x + padding),
                    cavernCentre.y + ruin.CavernHeight + 24,
                    math.max(cavernCentre.z + padding, cavernCentre.z + ruinReach.z + padding)),
                regionVoxelEdge);

            int3[] result = new int3[regions.Count];
            regions.CopyTo(result);
            Array.Sort(result, CompareRegions);
            return result;
        }

        public static UndergroundCavernLocalLight[] BuildLocalLights(
            MineCaveLightRequest[] requests,
            float voxelSizeMetres,
            float radiusMetres,
            float4 colourAndIntensity)
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (voxelSizeMetres <= 0f) throw new ArgumentOutOfRangeException(nameof(voxelSizeMetres));
            if (radiusMetres <= 0f) throw new ArgumentOutOfRangeException(nameof(radiusMetres));
            if (colourAndIntensity.w <= 0f)
                throw new ArgumentOutOfRangeException(nameof(colourAndIntensity));

            var result = new UndergroundCavernLocalLight[requests.Length];
            for (int i = 0; i < requests.Length; i++)
            {
                result[i] = new UndergroundCavernLocalLight(
                    requests[i].PositionVoxels * voxelSizeMetres,
                    radiusMetres,
                    colourAndIntensity,
                    requests[i].Variant);
            }
            return result;
        }

        private static void AddRegionBounds(
            HashSet<int3> regions,
            int3 minVoxel,
            int3 maxVoxel,
            int edge)
        {
            int3 first = RegionAtVoxel(minVoxel, edge);
            int3 last = RegionAtVoxel(maxVoxel, edge);
            for (int y = first.y; y <= last.y; y++)
            for (int z = first.z; z <= last.z; z++)
            for (int x = first.x; x <= last.x; x++)
                regions.Add(new int3(x, y, z));
        }

        private static int3 RegionAtVoxel(int3 voxel, int edge) => new int3(
            FloorDiv(voxel.x, edge),
            FloorDiv(voxel.y, edge),
            FloorDiv(voxel.z, edge));

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        private static int CompareRegions(int3 a, int3 b)
        {
            int compare = a.y.CompareTo(b.y);
            if (compare != 0) return compare;
            compare = a.z.CompareTo(b.z);
            return compare != 0 ? compare : a.x.CompareTo(b.x);
        }

        private static int3 FacingVector(Facing facing)
        {
            switch (facing)
            {
                case Facing.East: return new int3(1, 0, 0);
                case Facing.South: return new int3(0, 0, -1);
                case Facing.West: return new int3(-1, 0, 0);
                default: return new int3(0, 0, 1);
            }
        }
    }
}
