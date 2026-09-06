using Unity.Collections;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// CPU-visible result of the GPU page-allocation/write transaction.
    ///
    /// This is deliberately tiny bookkeeping, not geometry readback. The GPU already writes one
    /// status/identity record per batch item; production consumes that record only to decide
    /// whether pending geometry may proceed to renderer-demand approval, must be retried, or is a
    /// permanent capacity/configuration failure.
    /// </summary>
    internal enum GpuPagedBatchOutcomeKind : byte
    {
        ReadyCandidate,
        Unsupported,
        Exhausted,
        Stale,
        TooLarge,
        IdentityMismatch,
        Failed,
    }

    internal readonly struct GpuPagedBatchOutcome
    {
        internal const uint AllocationReady = 0u;
        internal const uint AllocationExhausted = 1u;
        internal const uint AllocationStale = 2u;
        internal const uint AllocationTooLarge = 3u;
        internal const uint AllocationUnsupported = 4u;

        private const int UnsupportedWord = 0;
        private const int StatusWord = 10;
        private const int HandleWord = 11;
        private const int GenerationLowWord = 12;
        private const int GenerationHighWord = 13;

        internal readonly GpuPagedBatchOutcomeKind Kind;
        internal readonly int Handle;
        internal readonly ulong Generation;
        internal readonly uint UnsupportedMask;

        private GpuPagedBatchOutcome(
            GpuPagedBatchOutcomeKind kind, int handle, ulong generation,
            uint unsupportedMask = 0u)
        {
            Kind = kind;
            Handle = handle;
            Generation = generation;
            UnsupportedMask = unsupportedMask;
        }

        internal bool IsReadyCandidate => Kind == GpuPagedBatchOutcomeKind.ReadyCandidate;
        internal bool IsRetryable =>
            Kind == GpuPagedBatchOutcomeKind.Exhausted
            || Kind == GpuPagedBatchOutcomeKind.Stale
            || Kind == GpuPagedBatchOutcomeKind.Failed;

        internal static GpuPagedBatchOutcome Parse(
            NativeArray<uint> words, int record, in GpuChunkExtraction expected)
        {
            if (!words.IsCreated || !TryRecordStart(words.Length, record, out int start))
                return FailedFor(in expected);
            uint unsupported = words[start + UnsupportedWord];
            uint status = words[start + StatusWord];
            int handle = unchecked((int)words[start + HandleWord]);
            ulong generation = words[start + GenerationLowWord]
                             | ((ulong)words[start + GenerationHighWord] << 32);
            return FromValues(unsupported, status, handle, generation, in expected);
        }

        internal static GpuPagedBatchOutcome Parse(
            uint[] words, int record, in GpuChunkExtraction expected)
        {
            if (words == null || !TryRecordStart(words.Length, record, out int start))
                return FailedFor(in expected);
            uint unsupported = words[start + UnsupportedWord];
            uint status = words[start + StatusWord];
            int handle = unchecked((int)words[start + HandleWord]);
            ulong generation = words[start + GenerationLowWord]
                             | ((ulong)words[start + GenerationHighWord] << 32);
            return FromValues(unsupported, status, handle, generation, in expected);
        }

        private static bool TryRecordStart(int length, int record, out int start)
        {
            start = 0;
            if (record < 0) return false;
            start = GpuSurfaceExtractor.BatchHeaderWords
                  + record * GpuSurfaceExtractor.BatchRecordWords;
            return start >= 0 && start + GenerationHighWord < length;
        }

        private static GpuPagedBatchOutcome FromValues(
            uint unsupportedMask, uint status, int handle, ulong generation,
            in GpuChunkExtraction expected)
        {
            if (handle != expected.Handle || generation != expected.Generation)
                return new GpuPagedBatchOutcome(
                    GpuPagedBatchOutcomeKind.IdentityMismatch, handle, generation,
                    unsupportedMask);

            // Word zero is the semantic support contract. Allocation also has an explicit
            // Unsupported status so even a future producer that clears/moves the detailed mask
            // cannot turn unsupported work into an apparently successful empty candidate.
            if (unsupportedMask != 0u || status == AllocationUnsupported)
                return new GpuPagedBatchOutcome(
                    GpuPagedBatchOutcomeKind.Unsupported, handle, generation,
                    unsupportedMask);

            GpuPagedBatchOutcomeKind kind = status switch
            {
                AllocationReady => GpuPagedBatchOutcomeKind.ReadyCandidate,
                AllocationExhausted => GpuPagedBatchOutcomeKind.Exhausted,
                AllocationStale => GpuPagedBatchOutcomeKind.Stale,
                AllocationTooLarge => GpuPagedBatchOutcomeKind.TooLarge,
                _ => GpuPagedBatchOutcomeKind.Failed,
            };
            return new GpuPagedBatchOutcome(kind, handle, generation);
        }

        private static GpuPagedBatchOutcome FailedFor(in GpuChunkExtraction expected) =>
            new(GpuPagedBatchOutcomeKind.Failed, expected.Handle, expected.Generation);

        public override string ToString() =>
            $"{Kind} handle={Handle} generation={Generation} unsupported=0x{UnsupportedMask:X}";
    }
}
