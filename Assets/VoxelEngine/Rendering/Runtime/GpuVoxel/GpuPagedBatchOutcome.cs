using System;
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

        private const int StatusWord = 10;
        private const int HandleWord = 11;
        private const int GenerationLowWord = 12;
        private const int GenerationHighWord = 13;

        internal readonly GpuPagedBatchOutcomeKind Kind;
        internal readonly int Handle;
        internal readonly ulong Generation;

        private GpuPagedBatchOutcome(
            GpuPagedBatchOutcomeKind kind, int handle, ulong generation)
        {
            Kind = kind;
            Handle = handle;
            Generation = generation;
        }

        internal bool IsReadyCandidate => Kind == GpuPagedBatchOutcomeKind.ReadyCandidate;
        internal bool IsRetryable =>
            Kind == GpuPagedBatchOutcomeKind.Exhausted
            || Kind == GpuPagedBatchOutcomeKind.Stale
            || Kind == GpuPagedBatchOutcomeKind.Failed;

        internal static GpuPagedBatchOutcome Parse(
            NativeArray<uint> words, int record, in GpuChunkExtraction expected)
        {
            if (!words.IsCreated)
                return FailedFor(in expected);
            return ParseCore(words.Length, index => words[index], record, in expected);
        }

        internal static GpuPagedBatchOutcome Parse(
            uint[] words, int record, in GpuChunkExtraction expected)
        {
            if (words == null)
                return FailedFor(in expected);
            return ParseCore(words.Length, index => words[index], record, in expected);
        }

        private static GpuPagedBatchOutcome ParseCore(
            int length, Func<int, uint> read, int record,
            in GpuChunkExtraction expected)
        {
            if (record < 0)
                return FailedFor(in expected);
            int start = GpuSurfaceExtractor.BatchHeaderWords
                      + record * GpuSurfaceExtractor.BatchRecordWords;
            if (start < 0 || start + GenerationHighWord >= length)
                return FailedFor(in expected);

            uint status = read(start + StatusWord);
            int handle = unchecked((int)read(start + HandleWord));
            ulong generation = read(start + GenerationLowWord)
                             | ((ulong)read(start + GenerationHighWord) << 32);

            if (handle != expected.Handle || generation != expected.Generation)
                return new GpuPagedBatchOutcome(
                    GpuPagedBatchOutcomeKind.IdentityMismatch, handle, generation);

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
            $"{Kind} handle={Handle} generation={Generation}";
    }
}
