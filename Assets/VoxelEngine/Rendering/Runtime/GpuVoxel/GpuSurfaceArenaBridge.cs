using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Result of staging one compute-meshed chunk into the renderer's existing shared arena.
    /// The caller owns a Ready lease and decides when it atomically replaces the previously
    /// published representation.
    /// </summary>
    internal enum GpuSurfaceArenaBuildStatus
    {
        Ready = 0,
        Empty = 1,
        ArenaFull = 2,
        CountWriteMismatch = 3,
    }

    internal readonly struct GpuSurfaceArenaBuild
    {
        public readonly GpuSurfaceArenaBuildStatus Status;
        public readonly SurfaceGeometryLease Lease;
        public readonly int VertexCount;
        public readonly int IndexCount;

        public GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus status,
                                    in SurfaceGeometryLease lease,
                                    int vertexCount, int indexCount)
        {
            Status = status;
            Lease = lease;
            VertexCount = vertexCount;
            IndexCount = indexCount;
        }

        public bool IsReady => Status == GpuSurfaceArenaBuildStatus.Ready;
    }

    /// <summary>
    /// Cutover seam between the production GPU extraction context and the arena/draw path already
    /// used by the CPU renderer.
    ///
    /// The context has already consumed the CPU builder's exact brick snapshot, mirrored mixed
    /// payloads, pinned every GPU brick the dispatch depends on, and counted the output. This bridge
    /// acquires a *staging* lease without disturbing the old published lease, writes geometry
    /// directly into the arena's GPU buffers, then writes the indirect args record last. Any
    /// failure releases only the staging lease. The staged mirror pins are released on every exit.
    /// </summary>
    internal static class GpuSurfaceArenaBridge
    {
        /// <summary>
        /// Consumes the chunk currently staged in <paramref name="context"/>. A Ready result owns
        /// the returned arena lease; every other result owns nothing. The context is always released
        /// before this method returns, so mirror eviction cannot be blocked by an abandoned build.
        /// </summary>
        public static GpuSurfaceArenaBuild Build(GpuSurfaceExtractionContext context,
                                                 SurfaceGeometryArena arena)
        {
            GpuExtractionCounts counts = context.StagedCounts;
            try
            {
                if (counts.IsEmpty)
                {
                    return new GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus.Empty, default,
                                                    counts.VertexCount, counts.IndexCount);
                }

                if (!arena.TryAcquire(counts.VertexCount, counts.IndexCount,
                                      out SurfaceGeometryLease lease))
                {
                    return new GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus.ArenaFull, default,
                                                    counts.VertexCount, counts.IndexCount);
                }

                if (!context.TryWriteRange(
                        arena.Vertices, arena.Indices,
                        lease.VertexStart, lease.VertexCapacity,
                        lease.IndexStart, lease.IndexCapacity,
                        out int indexCount))
                {
                    arena.Release(lease);
                    return new GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus.CountWriteMismatch,
                                                    default,
                                                    counts.VertexCount, counts.IndexCount);
                }

                // Args are the publication record the draw path consumes. Writing them last makes
                // the staging lease invisible as drawable geometry until the payload is complete.
                arena.UploadArgs((uint)indexCount, lease);
                return new GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus.Ready, lease,
                                                counts.VertexCount, indexCount);
            }
            finally
            {
                context.Release();
            }
        }
    }
}
