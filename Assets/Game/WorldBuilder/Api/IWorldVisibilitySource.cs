using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;

namespace Game.WorldBuilder.Api
{
    /// <summary>
    /// Integer world-space bounds used to query renderer-neutral semantic visibility facts.
    /// Coordinates are deterministic decimetres; querying visibility never implies voxel residency.
    /// </summary>
    public readonly struct WorldVisibilityBoundsDm
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public WorldVisibilityBoundsDm(int minX, int minY, int maxX, int maxY)
        {
            if (maxX <= minX) throw new System.ArgumentOutOfRangeException(nameof(maxX));
            if (maxY <= minY) throw new System.ArgumentOutOfRangeException(nameof(maxY));
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Intersects(StructureFarPresentation value) =>
            value.FootprintMaxDm.X > MinX
            && value.FootprintMinDm.X < MaxX
            && value.FootprintMaxDm.Y > MinY
            && value.FootprintMinDm.Y < MaxY;
    }

    /// <summary>
    /// Read-only semantic far-world visibility source. Implementations expose lightweight planned
    /// descriptors only and must not require voxel generation, storage residency, render objects,
    /// collision, interiors, NPCs, or physics to answer a query.
    /// </summary>
    public interface IWorldVisibilitySource
    {
        bool TryGet(ulong structureKey, out StructureFarPresentation value);

        /// <summary>
        /// Returns intersecting descriptors in stable StructureKey order, independent of insertion
        /// order or the implementation's hash/sector traversal order.
        /// </summary>
        IReadOnlyList<StructureFarPresentation> Query(WorldVisibilityBoundsDm bounds);
    }
}
