using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Edits.Api;

namespace VoxelEngine.Core.Edits
{
    /// <summary>
    /// Compatibility expansion helper for canonical brush events.
    ///
    /// Live brush semantics are owned by BrushShapeCodec + DeterministicAlterationApplier. This
    /// helper exposes the same cube voxel coordinates for tooling/tests that still need expansion,
    /// but it no longer implements the old overlapping sphere/cylinder/extrude packing.
    /// </summary>
    public static class BrushExpansion
    {
        public const byte ShapeCube = BrushShapeCodec.ShapeCube;

        // Historical constants retained only so old source references compile. These shapes have no
        // canonical wire representation yet and therefore fail closed if passed to ExpandTyped.
        public const byte ShapeSphere = 2;
        public const byte ShapeCylinder = 3;
        public const byte ShapeExtrude = 4;

        /// <summary>Expand one canonical cube brush into exact world voxel coordinates.</summary>
        public static NativeList<int3> Expand(in BrickPool pool, in RegionTable table, AlterationEvent evt)
        {
            if (evt.kind != AlterationEvent.KindBrush ||
                !BrushShapeCodec.Validate(evt.shapeKind, evt.shapeData))
                throw new ArgumentException("Expected a canonical cube brush event.", nameof(evt));

            return ExpandCube(evt.origin, evt.BrushExtents());
        }

        /// <summary>
        /// Compatibility typed expansion. Only ShapeCube is live; other historical shape values are
        /// rejected until their deterministic packing/application semantics are specified.
        /// </summary>
        public static NativeList<int3> ExpandTyped(
            in BrickPool pool,
            in RegionTable table,
            byte shapeType,
            int3 origin,
            int3 extents,
            uint seed)
        {
            if (shapeType != ShapeCube)
                throw new NotSupportedException("Only canonical axis-aligned cube brushes are supported.");
            if (extents.x < 1 || extents.x > VoxelDimensions.RegionEdge ||
                extents.y < 1 || extents.y > VoxelDimensions.RegionEdge ||
                extents.z < 1 || extents.z > VoxelDimensions.RegionEdge)
                throw new ArgumentOutOfRangeException(nameof(extents));

            return ExpandCube(origin, extents);
        }

        private static NativeList<int3> ExpandCube(int3 origin, int3 extentsBricks)
        {
            BrushShapeCodec.GetCubeVoxelBounds(origin, extentsBricks, out int3 minVoxel, out int3 maxVoxel);
            long count = (long)(maxVoxel.x - minVoxel.x + 1) *
                         (maxVoxel.y - minVoxel.y + 1) *
                         (maxVoxel.z - minVoxel.z + 1);
            int capacity = count > int.MaxValue ? int.MaxValue : (int)count;
            var result = new NativeList<int3>(capacity, Allocator.Temp);

            for (int z = minVoxel.z; z <= maxVoxel.z; z++)
            for (int y = minVoxel.y; y <= maxVoxel.y; y++)
            for (int x = minVoxel.x; x <= maxVoxel.x; x++)
                result.Add(new int3(x, y, z));

            return result;
        }
    }
}
