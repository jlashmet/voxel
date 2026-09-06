using System;
using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSubmissionLifetimeTests
    {
        [Test]
        public void DisposalWaitsForEverySubmissionAndReleasesExactlyOnce()
        {
            int releases = 0;
            var lifetime = new GpuSubmissionLifetime(() => releases++);
            lifetime.Retain(); lifetime.Retain();
            lifetime.Dispose(); lifetime.Dispose();
            Assert.That(releases, Is.Zero);
            lifetime.Release();
            Assert.That(releases, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => lifetime.Retain());
            lifetime.Release(); lifetime.Dispose();
            Assert.That(releases, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => lifetime.Release());
        }

        [Test]
        public void CompletionBeforeDisposalDoesNotPrematurelyReleaseAnActiveOwner()
        {
            int releases = 0;
            var lifetime = new GpuSubmissionLifetime(() => releases++);
            lifetime.Retain(); lifetime.Release();
            Assert.That(releases, Is.Zero);
            lifetime.Retain(); lifetime.Release(); lifetime.Dispose();
            Assert.That(releases, Is.EqualTo(1));
        }
    }
}
