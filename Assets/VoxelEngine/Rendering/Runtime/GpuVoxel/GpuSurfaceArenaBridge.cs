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
    /// Cutover seam between the compute Transvoxel mesher and the arena/draw path already used by
    /// the CPU renderer.
    ///
    /// Count first, acquire a *staging* lease without disturbing the old published lease, then
    /// write geometry directly into the arena's GPU buffers. The indirect args record is written
    /// only after count and write agree exactly. Any failure releases the staging lease, so a
    /// failed GPU build cannot turn previously covered terrain into a hole.
    /// </summary>
    internal static class GpuSurfaceArenaBridge
    {
        public static GpuSurfaceArenaBuild Build(GpuSurfaceExtractor extractor,
                                                 GpuVoxelBrickMirror mirror,
                                                 GpuTransvoxelTables tables,
                                                 in GpuChunkExtraction request,
                                                 SurfaceGeometryArena arena)
        {
            GpuExtractionCounts counts = extractor.Count(mirror, tables, request);
            if (counts.VertexCount == 0 || counts.IndexCount == 0)
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

            GpuExtractionResult written = extractor.WriteRange(
                mirror, tables, request,
                arena.Vertices, arena.Indices,
                lease.VertexStart, lease.VertexCapacity,
                lease.IndexStart, lease.IndexCapacity);

            // Alignment slack in SurfaceGeometryArena means a broken count pass can sometimes emit
            // more than it counted without tripping the raw buffer-capacity overflow check. Treat
            // any count/write disagreement as a failed build; publishing it would make reservation
            // correctness depend on accidental alignment headroom.
            if (written.Overflowed
                || written.VertexCount != counts.VertexCount
                || written.IndexCount != counts.IndexCount)
            {
                arena.Release(lease);
                return new GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus.CountWriteMismatch,
                                                default,
                                                written.VertexCount, written.IndexCount);
            }

            // Args are the publication record the draw path consumes. Writing them last makes the
            // staging lease invisible as drawable geometry until the payload is complete.
            arena.UploadArgs((uint)written.IndexCount, lease);
            return new GpuSurfaceArenaBuild(GpuSurfaceArenaBuildStatus.Ready, lease,
                                            written.VertexCount, written.IndexCount);
        }
    }
}
