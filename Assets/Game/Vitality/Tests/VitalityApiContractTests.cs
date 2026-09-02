using System;
using Game.Characters.Api;
using Game.Vitality.Api;
using NUnit.Framework;

namespace Game.Vitality.Tests
{
    public sealed class VitalityApiContractTests
    {
        [Test]
        public void SnapshotCarriesStableCurrentTruthWithoutRuntimeBehavior()
        {
            var snapshot = new VitalitySnapshot(new CharacterId("character:hero"), 25, 100, false, 7);
            Assert.That(snapshot.CharacterId.Value, Is.EqualTo("character:hero"));
            Assert.That(snapshot.Current, Is.EqualTo(25));
            Assert.That(snapshot.Maximum, Is.EqualTo(100));
            Assert.That(snapshot.Defeated, Is.False);
            Assert.That(snapshot.Revision, Is.EqualTo(7));
        }

        [Test]
        public void SnapshotRejectsImpossibleBounds()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new VitalitySnapshot(new CharacterId("character:hero"), 101, 100, false, 1));
        }
    }
}
