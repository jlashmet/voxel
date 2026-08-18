using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Slot bookkeeping for the GPU brick mirror.
    ///
    /// Every failure these cover produces the same visible symptom — geometry built from one brick
    /// wearing another brick's coordinate — and none of them need a graphics device to reproduce,
    /// which is the reason the policy lives in a plain C# type.
    /// </summary>
    public sealed class GpuBrickSlotTableTests
    {
        private static VoxelBrickDelta Mixed(int x, ulong generation = 1, int slot = 0) =>
            VoxelBrickDelta.MixedAt(new int3(x, 0, 0), generation, slot);

        [Test]
        public void MixedBrickTakesASlotAndIsFoundByCoordinate()
        {
            var table = new GpuBrickSlotTable(4);

            Assert.AreEqual(GpuBrickAdmission.Admitted, table.TryAdmit(Mixed(1), out int slot));
            Assert.GreaterOrEqual(slot, 0);
            Assert.IsTrue(table.TryGetSlot(new int3(1, 0, 0), out int found));
            Assert.AreEqual(slot, found);
            Assert.AreEqual(1, table.ResidentCount);
        }

        [Test]
        public void EmptyAndUniformBricksNeverConsumeASlot()
        {
            var table = new GpuBrickSlotTable(4);

            Assert.AreEqual(GpuBrickAdmission.NoPayload,
                table.TryAdmit(VoxelBrickDelta.EmptyAt(new int3(1, 0, 0), 1), out _));
            Assert.AreEqual(GpuBrickAdmission.NoPayload,
                table.TryAdmit(VoxelBrickDelta.UniformAt(new int3(2, 0, 0), 1, 7), out _));

            Assert.AreEqual(0, table.ResidentCount,
                "Uniform and empty bricks carry no payload, so mirroring them would spend slots "
              + "on nothing. That asymmetry is what keeps a kilometre-scale world in budget.");
        }

        [Test]
        public void RepublishingTheSameGenerationKeepsTheSameSlot()
        {
            var table = new GpuBrickSlotTable(4);
            table.TryAdmit(Mixed(1, generation: 5), out int first);

            Assert.AreEqual(GpuBrickAdmission.Resident,
                table.TryAdmit(Mixed(1, generation: 5), out int again));
            Assert.AreEqual(first, again);
            Assert.AreEqual(1, table.ResidentCount);
        }

        [Test]
        public void NewerGenerationReplacesInPlace()
        {
            var table = new GpuBrickSlotTable(4);
            table.TryAdmit(Mixed(1, generation: 5), out int first);

            Assert.AreEqual(GpuBrickAdmission.Admitted,
                table.TryAdmit(Mixed(1, generation: 6), out int second));
            Assert.AreEqual(first, second, "An edit reuses the brick's slot rather than moving it.");
            Assert.IsTrue(table.TryGetGeneration(new int3(1, 0, 0), out ulong generation));
            Assert.AreEqual(6UL, generation);
        }

        [Test]
        public void OlderGenerationArrivingLateIsRejected()
        {
            var table = new GpuBrickSlotTable(4);
            table.TryAdmit(Mixed(1, generation: 9), out _);

            Assert.AreEqual(GpuBrickAdmission.Stale,
                table.TryAdmit(Mixed(1, generation: 8), out _),
                "Publication is asynchronous, so an older payload can arrive after a newer one. "
              + "Accepting it would overwrite fresh voxels with stale ones.");
            Assert.IsTrue(table.TryGetGeneration(new int3(1, 0, 0), out ulong generation));
            Assert.AreEqual(9UL, generation);
            Assert.AreEqual(1UL, table.StaleCount);
        }

        [Test]
        public void AFullTableEvictsTheColdestUnpinnedBrick()
        {
            var table = new GpuBrickSlotTable(2);
            table.TryAdmit(Mixed(1), out _);
            table.TryAdmit(Mixed(2), out _);
            table.Touch(new int3(1, 0, 0));   // 1 is now warmer than 2

            Assert.AreEqual(GpuBrickAdmission.Admitted, table.TryAdmit(Mixed(3), out _));
            Assert.IsFalse(table.TryGetSlot(new int3(2, 0, 0), out _));
            Assert.IsTrue(table.TryGetSlot(new int3(1, 0, 0), out _));
            Assert.AreEqual(1UL, table.EvictionCount);
        }

        [Test]
        public void PinnedBricksAreNeverEvicted()
        {
            var table = new GpuBrickSlotTable(2);
            table.TryAdmit(Mixed(1), out _);
            table.TryAdmit(Mixed(2), out _);
            table.Pin(new int3(1, 0, 0));
            table.Pin(new int3(2, 0, 0));

            Assert.AreEqual(GpuBrickAdmission.Full, table.TryAdmit(Mixed(3), out int slot),
                "Recycling a slot the render hierarchy still references is how a mirror draws one "
              + "brick's geometry at another brick's coordinate. Refusing is the safe answer.");
            Assert.AreEqual(-1, slot);
            Assert.AreEqual(1UL, table.RefusedCount);
            Assert.AreEqual(0UL, table.EvictionCount);
        }

        [Test]
        public void UnpinningMakesASlotAvailableAgain()
        {
            var table = new GpuBrickSlotTable(1);
            table.TryAdmit(Mixed(1), out _);
            table.Pin(new int3(1, 0, 0));
            Assert.AreEqual(GpuBrickAdmission.Full, table.TryAdmit(Mixed(2), out _));

            table.Unpin(new int3(1, 0, 0));

            Assert.AreEqual(GpuBrickAdmission.Admitted, table.TryAdmit(Mixed(2), out _));
            Assert.AreEqual(0, table.PinnedCount);
        }

        [Test]
        public void ABrickEmptiedByDestructionReleasesItsSlot()
        {
            var table = new GpuBrickSlotTable(4);
            table.TryAdmit(Mixed(1), out _);
            Assert.AreEqual(1, table.ResidentCount);

            table.TryAdmit(VoxelBrickDelta.EmptyAt(new int3(1, 0, 0), 2), out _);

            Assert.AreEqual(0, table.ResidentCount,
                "Blowing a brick open to air must free mirror memory, not leave geometry resident "
              + "that nothing can reach.");
            Assert.IsFalse(table.TryGetSlot(new int3(1, 0, 0), out _));
        }

        [Test]
        public void MaterialMaskRoundTripsAcrossAllFourWords()
        {
            var delta = VoxelBrickDelta.MixedAt(int3.zero, 1, 0);
            byte[] materials = { 0, 1, 63, 64, 127, 128, 191, 192, 255 };
            foreach (byte material in materials) delta.AddMaterial(material);

            foreach (byte material in materials)
                Assert.IsTrue(delta.ContainsMaterial(material), $"material {material} lost");

            Assert.IsFalse(delta.ContainsMaterial(2));
            Assert.IsFalse(delta.ContainsMaterial(200));
        }

        [Test]
        public void UniformBrickOfEmptyMaterialIsNotSolid()
        {
            VoxelBrickDelta air = VoxelBrickDelta.UniformAt(
                int3.zero, 1, VoxelGrid.MaterialEmpty);

            Assert.IsFalse(air.HasSolid);
            Assert.IsTrue(air.IsWellFormed);
            Assert.IsFalse(air.NeedsSlot);
        }

        [Test]
        public void ClearReturnsEverySlotIncludingPinnedOnes()
        {
            var table = new GpuBrickSlotTable(3);
            table.TryAdmit(Mixed(1), out _);
            table.TryAdmit(Mixed(2), out _);
            table.Pin(new int3(1, 0, 0));

            table.Clear();

            Assert.AreEqual(0, table.ResidentCount);
            Assert.AreEqual(0, table.PinnedCount);
            Assert.AreEqual(GpuBrickAdmission.Admitted, table.TryAdmit(Mixed(9), out _));
        }
    }
}
