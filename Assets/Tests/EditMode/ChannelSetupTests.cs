using NUnit.Framework;
using Unity.Networking.Transport;
using VoxelEngine.Net.Transport;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ChannelSetupTests
    {
        [Test]
        public void PipelineHandlesRemainDistinctEvenWhenReliableStageDefinitionsMatch()
        {
            var driver = NetworkDriver.Create(ChannelSetup.DefaultSettings());
            try
            {
                ChannelSetup channels = ChannelSetup.Create(ref driver);

                Assert.That(channels.Event.Equals(channels.Repair), Is.False,
                    "EVENT and REPAIR need independent reliable sequence/ack state; identical stage definitions must not alias one pipeline handle.");
                Assert.That(channels.Event.Equals(channels.Ephemeral), Is.False);
                Assert.That(channels.Event.Equals(channels.Bulk), Is.False);
                Assert.That(channels.Repair.Equals(channels.Ephemeral), Is.False);
                Assert.That(channels.Repair.Equals(channels.Bulk), Is.False);
                Assert.That(channels.Ephemeral.Equals(channels.Bulk), Is.False);
            }
            finally
            {
                if (driver.IsCreated) driver.Dispose();
            }
        }
    }
}
