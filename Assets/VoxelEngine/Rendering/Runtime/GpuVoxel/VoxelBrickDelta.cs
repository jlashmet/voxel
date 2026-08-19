using System;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// What a brick contributes to the GPU mirror.
    ///
    /// Most of a voxel world is air or solid rock, and storage is built so those cost nothing:
    /// only a mixed brick carries a per-voxel payload. The delta format
    /// keeps that asymmetry rather than flattening it — a region of uniform stone publishes one
    /// material byte per brick, not 2 KB per brick — because the whole memory argument for a
    /// kilometre-scale world depends on it.
    /// </summary>
    public enum VoxelBrickContent : byte
    {
        /// <summary>No solid voxels. The GPU needs no slot at all; any existing one is released.</summary>
        Empty = 0,

        /// <summary>Every voxel identical. Carried by <see cref="VoxelBrickDelta.UniformMaterial"/>.</summary>
        Uniform = 1,

        /// <summary>Per-voxel payload. The only kind that occupies a GPU brick slot.</summary>
        Mixed = 2,
    }

    /// <summary>
    /// One brick's worth of change to publish to the GPU mirror.
    ///
    /// This is metadata only. It names where the payload lives on the CPU rather than carrying it,
    /// so a frame's worth of deltas stays small enough to sort, batch and diff without copying voxel
    /// data around before the upload that actually needs it. The payload is read straight out of the
    /// authoritative brick pool's parallel arrays at <see cref="SourceSlot"/>.
    ///
    /// Layout matches specs' §8.1 requirement: logical coordinate, storage slot, content generation,
    /// occupancy/material summary, and the dirty flags for dependent levels.
    /// </summary>
    [Serializable]
    public struct VoxelBrickDelta : IEquatable<VoxelBrickDelta>
    {
        /// <summary>Logical brick coordinate in world brick space, not region-relative.</summary>
        public int3 Coordinate;

        /// <summary>
        /// Authoritative content generation this delta describes. The GPU records it alongside the
        /// slot so a publication that lands after a newer edit can be recognised as stale and
        /// dropped, rather than overwriting fresher data with older data.
        /// </summary>
        public ulong SourceGeneration;

        /// <summary>
        /// Authoritative brick-pool slot holding the payload, or -1 when there is none to read
        /// (<see cref="VoxelBrickContent.Empty"/> and <see cref="VoxelBrickContent.Uniform"/>).
        /// </summary>
        public int SourceSlot;

        public VoxelBrickContent Content;

        /// <summary>Material for a uniform brick. Meaningless for the other kinds.</summary>
        public byte UniformMaterial;

        /// <summary>
        /// Whether the brick contains any solid voxel at all, precomputed on the CPU.
        ///
        /// The GPU's hierarchy needs this to decide whether a node can be skipped outright, and
        /// answering it from the occupancy words would mean reading the payload on a path that
        /// otherwise never touches it.
        /// </summary>
        public bool HasSolid;

        /// <summary>
        /// Materials present in the brick, as a 256-bit set across four words. Meshing selects
        /// shader variants and texture pages from this without scanning 512 voxels, and it is cheap
        /// to maintain because edits already walk the cells.
        /// </summary>
        public ulong MaterialMask0;
        public ulong MaterialMask1;
        public ulong MaterialMask2;
        public ulong MaterialMask3;

        /// <summary>True when this delta must also invalidate coarser mips that summarise it.</summary>
        public bool InvalidatesMips;

        public bool NeedsSlot => Content == VoxelBrickContent.Mixed;

        public bool IsWellFormed =>
            SourceGeneration != 0
            && (Content != VoxelBrickContent.Mixed || SourceSlot >= 0)
            && (Content != VoxelBrickContent.Empty || !HasSolid);

        public bool ContainsMaterial(byte material) => material switch
        {
            < 64 => (MaterialMask0 & (1UL << material)) != 0,
            < 128 => (MaterialMask1 & (1UL << (material - 64))) != 0,
            < 192 => (MaterialMask2 & (1UL << (material - 128))) != 0,
            _ => (MaterialMask3 & (1UL << (material - 192))) != 0,
        };

        public void AddMaterial(byte material)
        {
            switch (material)
            {
                case < 64: MaterialMask0 |= 1UL << material; break;
                case < 128: MaterialMask1 |= 1UL << (material - 64); break;
                case < 192: MaterialMask2 |= 1UL << (material - 128); break;
                default: MaterialMask3 |= 1UL << (material - 192); break;
            }
        }

        public static VoxelBrickDelta EmptyAt(int3 coordinate, ulong generation) =>
            new()
            {
                Coordinate = coordinate,
                SourceGeneration = generation,
                SourceSlot = -1,
                Content = VoxelBrickContent.Empty,
                InvalidatesMips = true,
            };

        public static VoxelBrickDelta UniformAt(int3 coordinate, ulong generation, byte material)
        {
            var delta = new VoxelBrickDelta
            {
                Coordinate = coordinate,
                SourceGeneration = generation,
                SourceSlot = -1,
                Content = VoxelBrickContent.Uniform,
                UniformMaterial = material,
                HasSolid = material != VoxelGrid.MaterialEmpty,
                InvalidatesMips = true,
            };
            if (delta.HasSolid) delta.AddMaterial(material);
            return delta;
        }

        public static VoxelBrickDelta MixedAt(int3 coordinate, ulong generation, int sourceSlot) =>
            new()
            {
                Coordinate = coordinate,
                SourceGeneration = generation,
                SourceSlot = sourceSlot,
                Content = VoxelBrickContent.Mixed,
                HasSolid = true,
                InvalidatesMips = true,
            };

        public bool Equals(VoxelBrickDelta other) =>
            Coordinate.Equals(other.Coordinate)
            && SourceGeneration == other.SourceGeneration
            && SourceSlot == other.SourceSlot
            && Content == other.Content
            && UniformMaterial == other.UniformMaterial;

        public override bool Equals(object obj) => obj is VoxelBrickDelta other && Equals(other);

        public override int GetHashCode() =>
            unchecked((Coordinate.GetHashCode() * 397) ^ SourceGeneration.GetHashCode());

        public override string ToString() =>
            $"Brick{Coordinate} gen={SourceGeneration} {Content}"
          + (Content == VoxelBrickContent.Mixed ? $" slot={SourceSlot}" : string.Empty)
          + (Content == VoxelBrickContent.Uniform ? $" material={UniformMaterial}" : string.Empty);
    }
}
