using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Edits.Runtime
{
    /// <summary>
    /// Shared authoritative/client application of semantic alteration events.
    /// Geometry and iteration order live in Edits; physical storage transitions are owned by
    /// Storage through block-granular mutation views.
    /// </summary>
    public sealed class DeterministicAlterationApplier : IAlterationApplier
    {
        bool IAlterationApplier.Supports(in AlterationEvent evt) => Supports(in evt);

        bool IAlterationApplier.HasRequiredResidency(
            IRegionMutationStore storage, in AlterationEvent evt) =>
            HasRequiredResidency(storage, in evt);

        bool IAlterationApplier.HasRequiredResidencyExcept(
            IRegionMutationStore storage, in AlterationEvent evt, int3 excludedRegion) =>
            HasRequiredResidencyExcept(storage, in evt, excludedRegion);

        bool IAlterationApplier.TryApply(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            out NativeList<int3> affectedBlocks) =>
            TryApply(storage, in evt, out affectedBlocks);

        bool IAlterationApplier.TryApplyExceptRegion(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            int3 excludedRegion,
            out NativeList<int3> affectedBlocks) =>
            TryApplyExceptRegion(storage, in evt, excludedRegion, out affectedBlocks);

        public static bool Supports(in AlterationEvent evt) =>
            evt.kind == AlterationEvent.KindExplosion ||
            (evt.kind == AlterationEvent.KindBrush && BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData));

        public static bool HasRequiredResidency(IRegionMutationStore storage, in AlterationEvent evt) =>
            HasRequiredResidencyInternal(storage, in evt, false, default);

        public static bool HasRequiredResidencyExcept(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            int3 excludedRegion) =>
            HasRequiredResidencyInternal(storage, in evt, true, excludedRegion);

        private static bool HasRequiredResidencyInternal(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            bool hasExcludedRegion,
            int3 excludedRegion)
        {
            if (storage == null || !TryGetVoxelBounds(in evt, out int3 minVoxel, out int3 maxVoxel))
                return false;

            int3 minRegion = minVoxel >> VoxelGrid.RegionVoxelEdgeLog2;
            int3 maxRegion = maxVoxel >> VoxelGrid.RegionVoxelEdgeLog2;

            for (int rz = minRegion.z; rz <= maxRegion.z; rz++)
            for (int ry = minRegion.y; ry <= maxRegion.y; ry++)
            for (int rx = minRegion.x; rx <= maxRegion.x; rx++)
            {
                int3 regionCoord = new int3(rx, ry, rz);
                if (hasExcludedRegion && regionCoord.Equals(excludedRegion))
                    continue;
                if (!storage.IsRegionResident(regionCoord))
                    return false;
            }

            return true;
        }

        public static bool TryApply(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            out NativeList<int3> affectedBricks)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                    return ApplyExplosion(storage, in evt, false, default, out affectedBricks);
                case AlterationEvent.KindBrush when BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData):
                    return ApplyCubeBrush(storage, in evt, false, default, out affectedBricks);
                default:
                    affectedBricks = new NativeList<int3>(0, Allocator.Temp);
                    return false;
            }
        }

        public static bool TryApplyExceptRegion(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            int3 excludedRegion,
            out NativeList<int3> affectedBricks)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                    return ApplyExplosion(storage, in evt, true, excludedRegion, out affectedBricks);
                case AlterationEvent.KindBrush when BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData):
                    return ApplyCubeBrush(storage, in evt, true, excludedRegion, out affectedBricks);
                default:
                    affectedBricks = new NativeList<int3>(0, Allocator.Temp);
                    return false;
            }
        }

        private static bool ApplyCubeBrush(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            bool hasExcludedRegion,
            int3 excludedRegion,
            out NativeList<int3> affectedBricks)
        {
            affectedBricks = new NativeList<int3>(64, Allocator.Temp);
            if (!BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData) ||
                !HasRequiredResidencyInternal(storage, in evt, hasExcludedRegion, excludedRegion))
                return false;

            BrushShapeCodec.GetCubeVoxelBounds(
                evt.origin,
                evt.BrushExtents(),
                out int3 minVoxel,
                out int3 maxVoxel);

            int3 minBrick = minVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 maxBrick = maxVoxel >> VoxelReadGrid.BlockEdgeLog2;
            byte targetMaterial = evt.material;
            bool markHardSurface = evt.BrushIsHardSurface() && targetMaterial != VoxelGrid.MaterialEmpty;
            bool anyChanged = false;

            for (int bz = minBrick.z; bz <= maxBrick.z; bz++)
            for (int by = minBrick.y; by <= maxBrick.y; by++)
            for (int bx = minBrick.x; bx <= maxBrick.x; bx++)
            {
                int3 worldBrick = new int3(bx, by, bz);
                int3 regionCoord = worldBrick >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
                if (hasExcludedRegion && regionCoord.Equals(excludedRegion))
                    continue;

                int3 brickMin = worldBrick << VoxelReadGrid.BlockEdgeLog2;
                int3 brickMax = brickMin + new int3(VoxelReadGrid.BlockEdge - 1);
                int3 writeMin = math.max(minVoxel, brickMin);
                int3 writeMax = math.min(maxVoxel, brickMax);
                bool wholeBrick = math.all(writeMin == brickMin) && math.all(writeMax == brickMax);

                bool changed = wholeBrick
                    ? storage.SetWholeBlock(worldBrick, targetMaterial, markHardSurface)
                    : SetPartialBrick(
                        storage,
                        worldBrick,
                        writeMin,
                        writeMax,
                        brickMin,
                        targetMaterial,
                        markHardSurface);

                if (!changed)
                    continue;

                affectedBricks.Add(worldBrick);
                anyChanged = true;
            }

            return anyChanged;
        }

        private static bool SetPartialBrick(
            IRegionMutationStore storage,
            int3 worldBrick,
            int3 writeMin,
            int3 writeMax,
            int3 brickMin,
            byte targetMaterial,
            bool markHardSurface)
        {
            if (!storage.TryBeginPartialBlock(
                    worldBrick, targetMaterial, markHardSurface, out VoxelBlockMutation mutation))
                return false;

            bool materialChanged = false;
            if (mutation.IsCreated)
            {
                int minX = writeMin.x - brickMin.x;
                int minY = writeMin.y - brickMin.y;
                int minZ = writeMin.z - brickMin.z;
                int maxX = writeMax.x - brickMin.x;
                int maxY = writeMax.y - brickMin.y;
                int maxZ = writeMax.z - brickMin.z;

                for (int z = minZ; z <= maxZ; z++)
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int voxelIndex = x
                                   | (y << VoxelReadGrid.BlockEdgeLog2)
                                   | (z << (VoxelReadGrid.BlockEdgeLog2 * 2));
                    materialChanged |= mutation.SetMaterial(voxelIndex, targetMaterial);
                }
            }

            return storage.CompletePartialBlock(ref mutation, materialChanged);
        }

        private static bool ApplyExplosion(
            IRegionMutationStore storage,
            in AlterationEvent evt,
            bool hasExcludedRegion,
            int3 excludedRegion,
            out NativeList<int3> affectedBricks)
        {
            affectedBricks = new NativeList<int3>(64, Allocator.Temp);
            int radiusVoxels = evt.Radius() * VoxelReadGrid.BlockEdge;
            if (radiusVoxels <= 0 ||
                !HasRequiredResidencyInternal(storage, in evt, hasExcludedRegion, excludedRegion))
                return false;

            long radiusSq = (long)radiusVoxels * radiusVoxels;
            int3 minVoxel = evt.origin - new int3(radiusVoxels);
            int3 maxVoxel = evt.origin + new int3(radiusVoxels);
            int3 minBrick = minVoxel >> VoxelReadGrid.BlockEdgeLog2;
            int3 maxBrick = maxVoxel >> VoxelReadGrid.BlockEdgeLog2;
            bool anyChanged = false;

            for (int bz = minBrick.z; bz <= maxBrick.z; bz++)
            for (int by = minBrick.y; by <= maxBrick.y; by++)
            for (int bx = minBrick.x; bx <= maxBrick.x; bx++)
            {
                int3 worldBrick = new int3(bx, by, bz);
                int3 brickMinVoxel = worldBrick << VoxelReadGrid.BlockEdgeLog2;
                int3 brickMaxVoxel = brickMinVoxel + new int3(VoxelReadGrid.BlockEdge - 1);

                if (DistanceSqToAabb(evt.origin, brickMinVoxel, brickMaxVoxel) > radiusSq)
                    continue;

                int3 regionCoord = worldBrick >> VoxelReadGrid.BlocksPerRegionEdgeLog2;
                if (hasExcludedRegion && regionCoord.Equals(excludedRegion))
                    continue;

                bool changed = FarthestCornerDistanceSq(evt.origin, brickMinVoxel, brickMaxVoxel) <= radiusSq
                    ? storage.SetWholeBlock(worldBrick, VoxelGrid.MaterialEmpty, false)
                    : ClearPartialBrick(storage, worldBrick, evt.origin, radiusSq, brickMinVoxel);

                if (!changed)
                    continue;

                affectedBricks.Add(worldBrick);
                anyChanged = true;
            }

            return anyChanged;
        }

        private static bool ClearPartialBrick(
            IRegionMutationStore storage,
            int3 worldBrick,
            int3 center,
            long radiusSq,
            int3 brickMinVoxel)
        {
            if (!storage.TryBeginPartialBlock(
                    worldBrick, VoxelGrid.MaterialEmpty, false, out VoxelBlockMutation mutation))
                return false;

            bool materialChanged = false;
            if (mutation.IsCreated)
            {
                for (int z = 0; z < VoxelReadGrid.BlockEdge; z++)
                {
                    long dz = (long)brickMinVoxel.z + z - center.z;
                    long dzSq = dz * dz;
                    for (int y = 0; y < VoxelReadGrid.BlockEdge; y++)
                    {
                        long dy = (long)brickMinVoxel.y + y - center.y;
                        long yzSq = dy * dy + dzSq;
                        if (yzSq > radiusSq)
                            continue;

                        for (int x = 0; x < VoxelReadGrid.BlockEdge; x++)
                        {
                            long dx = (long)brickMinVoxel.x + x - center.x;
                            if (dx * dx + yzSq > radiusSq)
                                continue;

                            int voxelIndex = x
                                           | (y << VoxelReadGrid.BlockEdgeLog2)
                                           | (z << (VoxelReadGrid.BlockEdgeLog2 * 2));
                            materialChanged |= mutation.SetMaterial(voxelIndex, VoxelGrid.MaterialEmpty);
                        }
                    }
                }
            }

            return storage.CompletePartialBlock(ref mutation, materialChanged);
        }

        private static bool TryGetVoxelBounds(in AlterationEvent evt, out int3 minVoxel, out int3 maxVoxel)
        {
            switch (evt.kind)
            {
                case AlterationEvent.KindExplosion:
                {
                    int radiusVoxels = evt.Radius() * VoxelReadGrid.BlockEdge;
                    if (radiusVoxels <= 0)
                    {
                        minVoxel = default;
                        maxVoxel = default;
                        return false;
                    }
                    minVoxel = evt.origin - new int3(radiusVoxels);
                    maxVoxel = evt.origin + new int3(radiusVoxels);
                    return true;
                }

                case AlterationEvent.KindBrush when BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData):
                    BrushShapeCodec.GetCubeVoxelBounds(evt.origin, evt.BrushExtents(), out minVoxel, out maxVoxel);
                    return true;

                default:
                    minVoxel = default;
                    maxVoxel = default;
                    return false;
            }
        }

        private static long DistanceSqToAabb(int3 point, int3 min, int3 max)
        {
            long dx = AxisDistance(point.x, min.x, max.x);
            long dy = AxisDistance(point.y, min.y, max.y);
            long dz = AxisDistance(point.z, min.z, max.z);
            return dx * dx + dy * dy + dz * dz;
        }

        private static long FarthestCornerDistanceSq(int3 point, int3 min, int3 max)
        {
            long dx = Max(Abs((long)point.x - min.x), Abs((long)max.x - point.x));
            long dy = Max(Abs((long)point.y - min.y), Abs((long)max.y - point.y));
            long dz = Max(Abs((long)point.z - min.z), Abs((long)max.z - point.z));
            return dx * dx + dy * dy + dz * dz;
        }

        private static long AxisDistance(int value, int min, int max)
        {
            if (value < min) return (long)min - value;
            if (value > max) return (long)value - max;
            return 0;
        }

        private static long Abs(long value) => value < 0 ? -value : value;
        private static long Max(long a, long b) => a > b ? a : b;
    }
}
