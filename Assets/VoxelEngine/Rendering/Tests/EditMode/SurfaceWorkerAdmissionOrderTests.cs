using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class SurfaceWorkerAdmissionOrderTests
    {
        [Test]
        public void ActiveWorkersLeadExistingBudgetWithRoundRobinFairness()
        {
            bool[] active = { false, true, false, true, true, false };
            int[] order = new int[active.Length];

            int count = SurfaceWorkerAdmissionOrder.Build(active, cursor: 2, order);

            Assert.That(count, Is.EqualTo(active.Length));
            CollectionAssert.AreEqual(new[] { 3, 4, 1, 2, 5, 0 }, order,
                "A tight frame budget must encounter every already-active owner before inactive admission.");

            int[] next = new int[active.Length];
            SurfaceWorkerAdmissionOrder.Build(active, cursor: 4, next);
            CollectionAssert.AreEqual(new[] { 4, 1, 3, 5, 0, 2 }, next,
                "Moving the cursor must rotate fairness inside both priority groups.");
        }

        [Test]
        public void NoActiveWorkersPreserveOriginalRoundRobinOrder()
        {
            bool[] active = { false, false, false, false, false };
            int[] order = new int[active.Length];

            SurfaceWorkerAdmissionOrder.Build(active, cursor: 2, order);

            CollectionAssert.AreEqual(new[] { 2, 3, 4, 0, 1 }, order);
        }
    }
}
