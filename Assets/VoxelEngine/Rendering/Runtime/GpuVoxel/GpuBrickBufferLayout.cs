using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// Where each brick's payload lives inside the mirror's GPU buffers.
    ///
    /// The mirror keeps the authoritative pool's shape: four parallel buffers indexed by slot, so
    /// publishing one brick is one write per buffer at one offset, and no channel has to be
    /// interleaved or repacked to find another. Storage hands out payloads as
    /// <see cref="PinnedVoxelReadBlock"/> with the same four channels, so a publish is a copy
    /// between two identically shaped things.
    ///
    /// Everything is expressed in 32-bit words because that is the only stride a structured
    /// ComputeBuffer takes. The counts work out exactly — 512 bytes is 128 words, 512 ushorts is
    /// 256 words, 8 occupancy ulongs is 16 words — so no padding is needed and a brick's payload
    /// can be reinterpreted in place rather than copied through a staging array.
    /// </summary>
    public static class GpuBrickBufferLayout
    {
        public const int VoxelsPerBrick = VoxelReadGrid.VoxelsPerBlock;
        public const int OccupancyWordsPerBrick = VoxelReadGrid.OccupancyWordsPerBlock;

        private const int BytesPerWord = 4;

        /// <summary>512 material bytes as 128 words.</summary>
        public const int MaterialWordsPerBrick = VoxelsPerBrick / BytesPerWord;

        /// <summary>512 surface-semantic ushorts as 256 words.</summary>
        public const int SurfaceWordsPerBrick = VoxelsPerBrick * sizeof(ushort) / BytesPerWord;

        /// <summary>512 authored-boundary bytes as 128 words.</summary>
        public const int BoundaryWordsPerBrick = VoxelsPerBrick / BytesPerWord;

        /// <summary>8 occupancy ulongs as 16 words.</summary>
        public const int OccupancyGpuWordsPerBrick =
            OccupancyWordsPerBrick * sizeof(ulong) / BytesPerWord;

        /// <summary>
        /// GPU bytes one mixed brick occupies across all four buffers. Mirrors the authoritative
        /// 2,112 B figure; empty and uniform bricks occupy none of it.
        /// </summary>
        public const int BytesPerMixedBrick =
            (MaterialWordsPerBrick + SurfaceWordsPerBrick
           + BoundaryWordsPerBrick + OccupancyGpuWordsPerBrick) * BytesPerWord;

        public static int MaterialWordOffset(int slot) => slot * MaterialWordsPerBrick;
        public static int SurfaceWordOffset(int slot) => slot * SurfaceWordsPerBrick;
        public static int BoundaryWordOffset(int slot) => slot * BoundaryWordsPerBrick;
        public static int OccupancyWordOffset(int slot) => slot * OccupancyGpuWordsPerBrick;

        /// <summary>Total GPU bytes a mirror of <paramref name="slotCapacity"/> bricks commits.</summary>
        public static long CommittedBytes(int slotCapacity) =>
            (long)slotCapacity * BytesPerMixedBrick;

        /// <summary>
        /// Slots a byte budget affords, at least one.
        ///
        /// Sizing the mirror by slot count rather than bytes would make the number meaningless
        /// across platforms; the device matrix speaks in bytes, so the conversion belongs here.
        /// </summary>
        public static int SlotsForBudget(long budgetBytes) =>
            budgetBytes <= BytesPerMixedBrick
                ? 1
                : (int)System.Math.Min(int.MaxValue, budgetBytes / BytesPerMixedBrick);
    }
}
