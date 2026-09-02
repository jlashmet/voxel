using Game.GameplayReplication.Api;
using NUnit.Framework;

namespace Game.GameplayReplication.Tests
{
    public sealed class GameplayReplicationContractTests
    {
        [Test]
        public void AuthoritativeRevision_IsMonotonicAndZeroIsNotReadyState()
        {
            var none = new GameplayRevision(0);
            var first = new GameplayRevision(1);
            var later = new GameplayRevision(2);

            Assert.That(none.IsValid, Is.False);
            Assert.That(first.IsValid, Is.True);
            Assert.That(first.CompareTo(later), Is.LessThan(0));
            Assert.That(later.CompareTo(first), Is.GreaterThan(0));
        }

        [Test]
        public void GameplayReady_IsSemanticAndCarriesAuthoritativeRevision()
        {
            var synchronizing = new GameplaySynchronizationStatus(
                GameplaySynchronizationPhase.Synchronizing,
                new GameplayRevision(4));
            var ready = new GameplaySynchronizationStatus(
                GameplaySynchronizationPhase.GameplayReady,
                new GameplayRevision(5));

            Assert.That(synchronizing.GameplayReady, Is.False);
            Assert.That(ready.GameplayReady, Is.True);
            Assert.That(ready.Revision, Is.EqualTo(new GameplayRevision(5)));
        }

        [Test]
        public void TypedCurrentProjection_CarriesStateAndRevisionWithoutTransportTypes()
        {
            var state = new FixtureState(42, 3);
            var snapshot = new GameplayProjectionSnapshot<FixtureState>(new GameplayRevision(9), state);

            Assert.That(snapshot.Revision, Is.EqualTo(new GameplayRevision(9)));
            Assert.That(snapshot.State.Vitality, Is.EqualTo(42));
            Assert.That(snapshot.State.InventoryCount, Is.EqualTo(3));
        }

        private readonly struct FixtureState
        {
            public int Vitality { get; }
            public int InventoryCount { get; }

            public FixtureState(int vitality, int inventoryCount)
            {
                Vitality = vitality;
                InventoryCount = inventoryCount;
            }
        }
    }
}
