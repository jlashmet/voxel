using NUnit.Framework;
using VoxelEngine.Showcase.Editor;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class MountainDragonSourceArchiveTests
    {
        [Test]
        public void ReconstructObjBytes_CommittedArchiveMatchesPinnedIdentity()
        {
            byte[] obj = MountainDragonSourceArchive.ReconstructObjBytes();

            Assert.That(obj, Has.Length.EqualTo(MountainDragonSourceArchive.ExpectedObjByteCount));
        }
    }
}
