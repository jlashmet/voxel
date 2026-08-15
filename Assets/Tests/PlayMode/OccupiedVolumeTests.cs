using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Net.Runtime.Server;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// SC-018: authoritative building must not intersect another player's collision volume.
    ///
    /// Player occupancy is session state owned by ServerPlayerRegistry; it is not inferred by
    /// asking Net to inspect physical voxel storage. AuthoritativeAlterationValidator consumes
    /// this predicate when validating constructive edits.
    /// </summary>
    public sealed class OccupiedVolumeTests
    {
        [Test]
        [Category("SC_018")]
        [Category("US3")]
        public void PlacementAtPlayerPositionIsRejectedByPlayerVolumePredicate()
        {
            var players = new ServerPlayerRegistry();
            Assert.That(players.TryRegisterAuthenticated(1, 7, new int3(5, 10, 5)), Is.True);

            Assert.That(
                players.IntersectsPlayerVolume(new int3(5, 10, 5), new int3(5, 10, 5)),
                Is.True,
                "A constructive edit at the player's authoritative position must intersect their volume.");
        }

        [Test]
        [Category("SC_018")]
        public void DistantPlacementDoesNotIntersectPlayerVolume()
        {
            var players = new ServerPlayerRegistry();
            Assert.That(players.TryRegisterAuthenticated(1, 7, new int3(10, 5, 10)), Is.True);

            Assert.That(
                players.IntersectsPlayerVolume(new int3(40, 40, 40), new int3(42, 42, 42)),
                Is.False);
        }

        [Test]
        [Category("SC_018")]
        public void ZeroHalfExtentsChecksOnlyAuthoritativePlayerVoxel()
        {
            var players = new ServerPlayerRegistry();
            Assert.That(players.TryRegisterAuthenticated(1, 7, new int3(5, 10, 5)), Is.True);
            Assert.That(players.SetCollisionHalfExtents(1, int3.zero), Is.True);

            Assert.That(
                players.IntersectsPlayerVolume(new int3(5, 10, 6), new int3(5, 10, 6)),
                Is.False);
            Assert.That(
                players.IntersectsPlayerVolume(new int3(5, 10, 5), new int3(5, 10, 5)),
                Is.True);
        }
    }
}
