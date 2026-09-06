using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuPagedBatchOutcomeTests
    {
        private const int Record = 1;
        private const int StatusWord = 10;
        private const int HandleWord = 11;
        private const int GenerationLowWord = 12;
        private const int GenerationHighWord = 13;

        private static GpuChunkExtraction Request(int handle = 7, ulong generation = 0x1020304050607080UL) =>
            new(int3.zero, int3.zero, sourceStep: 1, voxelSize: 0.1f,
                handle: handle, generation: generation);

        private static uint[] Words(uint status, in GpuChunkExtraction request)
        {
            var words = new uint[
                GpuSurfaceExtractor.BatchHeaderWords
                + (Record + 1) * GpuSurfaceExtractor.BatchRecordWords];
            int start = GpuSurfaceExtractor.BatchHeaderWords
                      + Record * GpuSurfaceExtractor.BatchRecordWords;
            words[start + StatusWord] = status;
            words[start + HandleWord] = unchecked((uint)request.Handle);
            words[start + GenerationLowWord] = (uint)request.Generation;
            words[start + GenerationHighWord] = (uint)(request.Generation >> 32);
            return words;
        }

        [TestCase(GpuPagedBatchOutcome.AllocationReady,
                  GpuPagedBatchOutcomeKind.ReadyCandidate, false)]
        [TestCase(GpuPagedBatchOutcome.AllocationExhausted,
                  GpuPagedBatchOutcomeKind.Exhausted, true)]
        [TestCase(GpuPagedBatchOutcome.AllocationStale,
                  GpuPagedBatchOutcomeKind.Stale, true)]
        [TestCase(GpuPagedBatchOutcome.AllocationTooLarge,
                  GpuPagedBatchOutcomeKind.TooLarge, false)]
        public void KnownAllocationStatusesRemainDistinct(
            uint status, GpuPagedBatchOutcomeKind expectedKind, bool retryable)
        {
            GpuChunkExtraction request = Request();
            GpuPagedBatchOutcome outcome =
                GpuPagedBatchOutcome.Parse(Words(status, in request), Record, in request);

            Assert.That(outcome.Kind, Is.EqualTo(expectedKind));
            Assert.That(outcome.Handle, Is.EqualTo(request.Handle));
            Assert.That(outcome.Generation, Is.EqualTo(request.Generation));
            Assert.That(outcome.IsRetryable, Is.EqualTo(retryable));
        }

        [Test]
        public void IdentityMismatchCannotBecomeReadyCandidate()
        {
            GpuChunkExtraction request = Request();
            uint[] words = Words(GpuPagedBatchOutcome.AllocationReady, in request);
            int start = GpuSurfaceExtractor.BatchHeaderWords
                      + Record * GpuSurfaceExtractor.BatchRecordWords;
            words[start + GenerationLowWord]++;

            GpuPagedBatchOutcome outcome =
                GpuPagedBatchOutcome.Parse(words, Record, in request);

            Assert.That(outcome.Kind, Is.EqualTo(GpuPagedBatchOutcomeKind.IdentityMismatch));
            Assert.That(outcome.IsReadyCandidate, Is.False);
            Assert.That(outcome.IsRetryable, Is.False,
                "An identity mismatch is a correctness failure, not blind retry permission.");
        }

        [Test]
        public void UnknownStatusIsFailureRatherThanSuccess()
        {
            GpuChunkExtraction request = Request();
            GpuPagedBatchOutcome outcome =
                GpuPagedBatchOutcome.Parse(Words(99u, in request), Record, in request);

            Assert.That(outcome.Kind, Is.EqualTo(GpuPagedBatchOutcomeKind.Failed));
            Assert.That(outcome.IsReadyCandidate, Is.False);
            Assert.That(outcome.IsRetryable, Is.True);
        }

        [Test]
        public void TruncatedBookkeepingCannotBecomeSuccess()
        {
            GpuChunkExtraction request = Request();
            var truncated = new uint[GpuSurfaceExtractor.BatchHeaderWords + 2];

            GpuPagedBatchOutcome outcome =
                GpuPagedBatchOutcome.Parse(truncated, Record, in request);

            Assert.That(outcome.Kind, Is.EqualTo(GpuPagedBatchOutcomeKind.Failed));
            Assert.That(outcome.IsReadyCandidate, Is.False);
        }
    }
}
