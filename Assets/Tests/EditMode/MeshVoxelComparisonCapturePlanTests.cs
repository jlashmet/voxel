using System.Collections.Generic;
using NUnit.Framework;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MeshVoxelComparisonCapturePlanTests
    {
        [Test]
        public void RequiredViews_CoverEveryAcceptanceViewExactlyOnce()
        {
            MeshVoxelCaptureView[] views = MeshVoxelComparisonCapturePlan.CreateRequiredViews();
            Assert.That(views, Has.Length.EqualTo(10));

            var ids = new HashSet<string>();
            int overall = 0;
            int head = 0;
            int wing = 0;
            int feet = 0;
            int tail = 0;
            int elevated = 0;

            for (int i = 0; i < views.Length; i++)
            {
                Assert.That(ids.Add(views[i].Id), Is.True, $"Duplicate capture id {views[i].Id}.");
                switch (views[i].Subject)
                {
                    case MeshVoxelCaptureSubject.Overall: overall++; break;
                    case MeshVoxelCaptureSubject.HeadHorns: head++; break;
                    case MeshVoxelCaptureSubject.Wing: wing++; break;
                    case MeshVoxelCaptureSubject.FeetClaws: feet++; break;
                    case MeshVoxelCaptureSubject.Tail: tail++; break;
                }
                if (views[i].Elevated) elevated++;
            }

            Assert.That(overall, Is.EqualTo(6));
            Assert.That(head, Is.EqualTo(1));
            Assert.That(wing, Is.EqualTo(1));
            Assert.That(feet, Is.EqualTo(1));
            Assert.That(tail, Is.EqualTo(1));
            Assert.That(elevated, Is.EqualTo(1));

            Assert.That(ids, Does.Contain("front"));
            Assert.That(ids, Does.Contain("side"));
            Assert.That(ids, Does.Contain("rear"));
            Assert.That(ids, Does.Contain("front-three-quarter"));
            Assert.That(ids, Does.Contain("rear-three-quarter"));
            Assert.That(ids, Does.Contain("elevated-top-three-quarter"));
        }
    }
}
