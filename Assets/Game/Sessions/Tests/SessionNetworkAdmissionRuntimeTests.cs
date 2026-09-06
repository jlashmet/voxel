using System.Diagnostics;
using System.Threading;
using Game.Sessions.Validation;
using NUnit.Framework;

namespace Game.Sessions.Tests
{
    public sealed class SessionNetworkAdmissionRuntimeTests
    {
        [Test]
        [Category("Networking")]
        public void RealNetworkAdmissionPreservesSessionsThroughRejectionRetryAndReconnect()
        {
            using var probe = new SessionNetworkAdmissionProbe();
            var clock = Stopwatch.StartNew();
            while (!probe.Complete && clock.ElapsedMilliseconds < 5000)
            {
                probe.Step();
                Thread.Yield();
            }
            Assert.That(probe.Complete, Is.True, "Monotonic deadline expired in " + probe.PhaseDescription);
            Assert.That(probe.RejectionPreservedState, Is.True);
            Assert.That(probe.DuplicatePreservedState, Is.True);
            Assert.That(probe.ReconnectPreservedIdentity, Is.True);
        }
    }
}
