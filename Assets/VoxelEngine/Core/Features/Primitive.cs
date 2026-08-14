using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Core.Features
{
    /// <summary>What a primitive does to the voxels it covers.</summary>
    public enum PrimitiveMode : byte
    {
        /// <summary>Writes its material unconditionally.</summary>
        Fill = 0,

        /// <summary>Clears to empty. How interiors, caves, doorways, and windows are expressed.</summary>
        Carve = 1,

        /// <summary>Writes only where the voxel is currently empty. Used to avoid overwriting detail.</summary>
        FillIfEmpty = 2,

        /// <summary>
        /// Repaints every existing solid voxel inside the primitive without changing occupancy.
        /// Useful for material-only edits to an already-bounded solid volume.
        /// </summary>
        PaintSolid = 3,

        /// <summary>
        /// For each horizontal column covered by the primitive, finds the highest solid voxel and
        /// repaints the top four contiguous solid voxels without changing occupancy. This is the
        /// terrain/biome operation: the material follows the actual density surface instead of a
        /// guessed height band, while shallow repainting preserves mineral support underneath.
        /// </summary>
        PaintSurface = 4,

        /// <summary>
        /// Writes only generic surface-detail strength/style on existing solids. Occupancy,
        /// base material, coating, and authored boundary geometry remain unchanged.
        /// </summary>
        SurfaceDetail = 5,
    }

    public enum PrimitiveShape : byte
    {
        Box = 0,
        Cylinder = 1,
        Prism = 2,
        Capsule = 3,
        Ramp = 4,
        RoundedBox = 5,
        Ellipsoid = 6,
        Frustum = 7,
        Annulus = 8,
        ArcWedge = 9,
    }

    /// <summary>Profile for <see cref="PrimitiveShape.Prism"/>.</summary>
    public enum PrismProfile : byte
    {
        Gable = 0,
        Shed = 1,
        Arch = 2,
    }

    /// <summary>
    /// The intermediate representation between a feature definition and voxels.
    ///
    /// A shape program emits primitives; the rasteriser turns them into voxels. That indirection
    /// is the whole reason this design can generate a castle one region at a time: a primitive can
    /// be clipped to a sub-volume analytically, so a region pays for the part of a feature that
    /// overlaps it rather than for the feature.
    ///
    /// It also gives the far field something to draw. Primitives rasterise at any resolution, so a
    /// distant castle can be rendered coarsely without any voxel ever existing for it.
    ///
    /// Blittable and integer-only so it can live in a NativeArray inside a Burst job.
    /// </summary>
    public struct Primitive
    {
        public PrimitiveShape Shape;
        public PrimitiveMode Mode;

        /// <summary>Palette index. Ignored when <see cref="Mode"/> is <see cref="PrimitiveMode.Carve"/>.</summary>
        public byte Material;

        /// <summary>Independent reconstruction style and optional visual coating.</summary>
        public ushort SurfaceStyle;
        public byte Coating;
        public VoxelSurfaceFlags SurfaceFlags;
        public byte SurfaceDetail;

        /// <summary>Axis for cylinders and ramps: 0 = x, 1 = y, 2 = z.</summary>
        public byte Axis;

        /// <summary>
        /// Signed shape direction after cardinal orientation. For ramps/frusta this is the axis
        /// direction; for prisms it is the horizontal profile direction. Zero is treated as +1
        /// for compatibility with default-constructed primitives.
        /// </summary>
        public sbyte Direction;

        public PrismProfile Profile;

        /// <summary>
        /// Within-instance ordering. Later primitives win where they overlap earlier ones, which
        /// is how a window carves a wall that was filled a moment before.
        /// </summary>
        public int Order;

        /// <summary>Inclusive minimum corner in world voxels; for a capsule, the first endpoint.</summary>
        public int3 A;

        /// <summary>Inclusive maximum corner in world voxels; for a capsule, the second endpoint.</summary>
        public int3 B;

        /// <summary>Radius in voxels for capsules.</summary>
        public int Radius;

        /// <summary>Second radius for annuli/frusta and shape-specific integer parameters.</summary>
        public int InnerRadius;
        public int3 C;
        public int3 D;
        public int2 StartDirection;
        public int2 EndDirection;

        /// <summary>Axis-aligned bounds, including a capsule's radius. Used for clipping and budgeting.</summary>
        public void Bounds(out int3 min, out int3 max)
        {
            min = math.min(A, B);
            max = math.max(A, B);

            if (Shape == PrimitiveShape.Capsule)
            {
                min -= Radius;
                max += Radius;
            }
        }

        /// <summary>True when this primitive touches the half-open volume [min, max).</summary>
        public bool Intersects(int3 volumeMin, int3 volumeMax)
        {
            Bounds(out var min, out var max);

            return min.x < volumeMax.x && max.x >= volumeMin.x
                && min.y < volumeMax.y && max.y >= volumeMin.y
                && min.z < volumeMax.z && max.z >= volumeMin.z;
        }
    }
}
