using Unity.Mathematics;

namespace VoxelEngine.Storage.Api
{
    /// <summary>
    /// Logical semantic snapshot produced by Storage for convergence/current-state replication.
    /// The encoded bytes and hash describe voxel semantics only; allocator slots and physical
    /// brick representation never cross this boundary.
    /// </summary>
    public readonly struct RegionSemanticSnapshot
    {
        public readonly int3 RegionCoord;
        public readonly uint SemanticHash;
        public readonly byte[] Bytes;

        public RegionSemanticSnapshot(int3 regionCoord, uint semanticHash, byte[] bytes)
        {
            RegionCoord = regionCoord;
            SemanticHash = semanticHash;
            Bytes = bytes;
        }
    }

    /// <summary>
    /// Storage-owned semantic snapshot capability used by networking/convergence clients.
    /// </summary>
    public interface IRegionSnapshotSource
    {
        bool TryCaptureSemanticSnapshot(
            int3 regionCoord,
            int maxBytes,
            out RegionSemanticSnapshot snapshot);
    }
}
