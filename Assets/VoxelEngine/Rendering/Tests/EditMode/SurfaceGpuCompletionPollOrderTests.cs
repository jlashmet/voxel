using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class SurfaceGpuCompletionPollOrderTests
    {
        [Test]
        public void PagedGpuCompletionWorkersLeadExistingBudgetWithoutReorderingPeers()
        {
            int[] phases = { 0, 9, -1, 4, 9, 2 };
            int[] order = new int[phases.Length];

            int count = SurfaceGpuCompletionPollOrder.Build(phases, cursor: 2, order);

            Assert.That(count, Is.EqualTo(phases.Length));
            CollectionAssert.AreEqual(new[] { 4, 1, 2, 3, 5, 0 }, order,
                "Fence-complete paged owners must be polled before ordinary work while each group keeps cursor order.");

            int[] next = new int[phases.Length];
            SurfaceGpuCompletionPollOrder.Build(phases, cursor: 4, next);
            CollectionAssert.AreEqual(new[] { 4, 1, 5, 0, 2, 3 }, next,
                "Moving the cursor must preserve round-robin fairness inside both groups.");
        }

        [Test]
        public void PagedGpuCompletionRemainsVisitableAfterOrdinaryBudgetExpires()
        {
            Assert.That(
                SurfaceGpuCompletionPollOrder.CanVisit(
                    SurfaceGpuCompletionPollOrder.PagedGpuCompletionPhase,
                    remainingBudgetMs: 0.0),
                Is.True,
                "A fence-complete paged owner must still get its non-blocking retirement poll when ordinary admission spent the shared deadline.");
            Assert.That(
                SurfaceGpuCompletionPollOrder.CanVisit(
                    SurfaceGpuCompletionPollOrder.PagedGpuCompletionPhase,
                    remainingBudgetMs: -0.25),
                Is.True,
                "Completion retirement must not depend on positive ordinary admission budget.");
        }

        [Test]
        public void OrdinaryWorkersStopWhenAdmissionBudgetExpires()
        {
            Assert.That(SurfaceGpuCompletionPollOrder.CanVisit(0, remainingBudgetMs: 0.0), Is.False);
            Assert.That(SurfaceGpuCompletionPollOrder.CanVisit(4, remainingBudgetMs: -0.25), Is.False);
            Assert.That(SurfaceGpuCompletionPollOrder.CanVisit(-1, remainingBudgetMs: 0.25), Is.True);
        }

        [Test]
        public void NoPagedGpuCompletionPreservesOriginalRoundRobinOrder()
        {
            int[] phases = { 0, 2, -1, 4, 3 };
            int[] order = new int[phases.Length];

            SurfaceGpuCompletionPollOrder.Build(phases, cursor: 2, order);

            CollectionAssert.AreEqual(new[] { 2, 3, 4, 0, 1 }, order);
        }
    }
}
