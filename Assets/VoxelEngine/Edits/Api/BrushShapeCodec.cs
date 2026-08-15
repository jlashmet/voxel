using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Edits.Api
{
    /// <summary>
    /// Canonical deterministic brush shape packing shared by requests, authoritative events,
    /// validation and application.
    ///
    /// shapeKind bytes (little-endian logical layout):
    ///   bits  0..7  extent X in bricks
    ///   bits  8..15 extent Y in bricks
    ///   bits 16..23 extent Z in bricks
    ///   bits 24..31 shape type
    ///
    /// shapeData currently carries only semantic flags. Keeping dimensions byte-sized is lossless
    /// because one region edge is 64 bricks, and it removes the previous overlap where extent Y and
    /// the shape discriminator both occupied bits 24..31.
    /// </summary>
    public static class BrushShapeCodec
    {
        public const byte ShapeCube = 1;

        public const uint FlagHardSurface = 1u << 0;
        public const uint KnownFlags = FlagHardSurface;

        public static uint PackCube(byte extentXBricks, byte extentYBricks, byte extentZBricks) =>
            (uint)extentXBricks |
            ((uint)extentYBricks << 8) |
            ((uint)extentZBricks << 16) |
            ((uint)ShapeCube << 24);

        public static byte ShapeType(uint shapeKind) => (byte)(shapeKind >> 24);

        public static int3 ExtentsBricks(uint shapeKind) => new int3(
            (byte)shapeKind,
            (byte)(shapeKind >> 8),
            (byte)(shapeKind >> 16));

        public static bool IsHardSurface(uint shapeData) => (shapeData & FlagHardSurface) != 0;

        public static bool Validate(uint shapeKind, uint shapeData)
        {
            if (ShapeType(shapeKind) != ShapeCube || (shapeData & ~KnownFlags) != 0)
                return false;

            int3 extents = ExtentsBricks(shapeKind);
            return extents.x >= 1 && extents.x <= VoxelReadGrid.BlocksPerRegionEdge &&
                   extents.y >= 1 && extents.y <= VoxelReadGrid.BlocksPerRegionEdge &&
                   extents.z >= 1 && extents.z <= VoxelReadGrid.BlocksPerRegionEdge;
        }

        /// <summary>
        /// Exact inclusive voxel bounds for a cube brush. Extents are full dimensions in bricks,
        /// not radii. Even dimensions use a deterministic lower-side bias around the voxel origin.
        /// </summary>
        public static void GetCubeVoxelBounds(
            int3 origin,
            int3 extentsBricks,
            out int3 minVoxel,
            out int3 maxVoxel)
        {
            int3 sizeVoxels = extentsBricks * VoxelReadGrid.BlockEdge;
            minVoxel = origin - (sizeVoxels >> 1);
            maxVoxel = minVoxel + sizeVoxels - 1;
        }
    }
}
