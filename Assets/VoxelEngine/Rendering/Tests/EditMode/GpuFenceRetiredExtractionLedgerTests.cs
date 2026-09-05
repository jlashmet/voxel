using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuFenceRetiredExtractionLedgerTests
    {
        [Test]
        public void SameFootprintRetiresExactlyOncePerFenceOwnedRecord()
        {
            var ledger = new GpuFenceRetiredExtractionLedger();
            int3 origin = new(12, -4, 30);

            ledger.Record(origin, 18);
            ledger.Record(origin, 18);

            Assert.That(ledger.Count, Is.EqualTo(2));
            Assert.That(ledger.TryConsume(origin, 18), Is.True);
            Assert.That(ledger.TryConsume(origin, 18), Is.True);
            Assert.That(ledger.TryConsume(origin, 18), Is.False,
                "Worker release must not retire shared extraction ownership twice.");
            Assert.That(ledger.Count, Is.Zero);
        }

        [Test]
        public void IndependentFootprintsCannotConsumeEachOthersFenceRetirement()
        {
            var ledger = new GpuFenceRetiredExtractionLedger();
            int3 first = new(1, 2, 3);
            int3 second = new(4, 5, 6);

            ledger.Record(first, 18);

            Assert.That(ledger.TryConsume(second, 18), Is.False);
            Assert.That(ledger.TryConsume(first, 17), Is.False);
            Assert.That(ledger.TryConsume(first, 18), Is.True);
            Assert.That(ledger.Count, Is.Zero);
        }

        [Test]
        public void ClearDropsOutstandingFenceRetirementsOnWorldReset()
        {
            var ledger = new GpuFenceRetiredExtractionLedger();
            int3 origin = new(-9, 0, 11);
            ledger.Record(origin, 18);

            ledger.Clear();

            Assert.That(ledger.Count, Is.Zero);
            Assert.That(ledger.TryConsume(origin, 18), Is.False);
        }
    }
}
