using System;
using System.IO;
using Game.Characters.Api;
using Game.Kentridge.PlayableSlice;
using Game.Persistence.Api;
using Game.Vitality.Api;
using Game.Vitality.Runtime;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeVitalityPersistenceTests
    {
        private static readonly CharacterId Player = CharacterId.FromStableKey("player", "persistence-fixture");

        [TestCase(3)]
        [TestCase(10)]
        public void RoundTripPreservesDamageDefeatAndRevisionWithoutReplayingEvents(int damage)
        {
            var source = new VitalityRegistry();
            Assert.That(source.Register(VitalitySnapshot.Alive(Player, 10)), Is.True);
            Assert.That(source.ApplyDamage(new DamageRequest(Player, damage)).Accepted, Is.True);
            Assert.That(source.TryGet(Player, out VitalitySnapshot expected), Is.True);
            IVitalityQuery current = source;
            ISessionSnapshotContributor contributor = KentridgeVitalityPersistence.CreateContributor(
                () => current, () => Player);
            SessionContributorCapture captured = contributor.Capture(42UL);
            Assert.That(captured.Succeeded, Is.True, captured.Detail);
            Assert.That(captured.Section.AuthoritativeRevision, Is.EqualTo(42UL));
            Assert.That(contributor.RequiredForRestore, Is.True);

            var restored = new VitalityRegistry();
            Assert.That(restored.Register(VitalitySnapshot.Alive(Player, 10)), Is.True);
            int defeats = 0;
            restored.Defeated += _ => defeats++;
            current = restored;
            Assert.That(contributor.Validate(captured.Section).Succeeded, Is.True);
            Assert.That(contributor.Restore(captured.Section).Succeeded, Is.True);
            Assert.That(restored.TryGet(Player, out VitalitySnapshot actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(defeats, Is.Zero, "Restore must not replay historical defeat events.");

            if (!actual.Defeated)
            {
                DamageResult further = restored.ApplyDamage(new DamageRequest(Player, 1));
                Assert.That(further.Accepted, Is.True);
                Assert.That(further.State.Current, Is.EqualTo(expected.Current - 1));
                Assert.That(further.State.Revision, Is.EqualTo(expected.Revision + 1UL));
                Assert.That(source.TryGet(Player, out VitalitySnapshot original), Is.True);
                Assert.That(original, Is.EqualTo(expected), "The old graph must not receive restored gameplay writes.");
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(65537)]
        public void RejectsInvalidCountsWithoutMutatingTheTarget(int count)
        {
            AssertRejected(Payload(writer => writer.Write(count)));
        }

        [Test]
        public void RejectsDuplicateCharacterStateWithoutMutatingTheTarget()
        {
            AssertRejected(Payload(writer =>
            {
                writer.Write(2);
                WriteState(writer, Player, 7, 10, false, 1UL);
                WriteState(writer, Player, 6, 10, false, 2UL);
            }));
        }

        [Test]
        public void RejectsMissingPlayerWithoutMutatingTheTarget()
        {
            AssertRejected(Payload(writer =>
            {
                writer.Write(1);
                WriteState(writer, CharacterId.FromStableKey("npc", "other"), 5, 10, false, 1UL);
            }));
        }

        [Test]
        public void RejectsInconsistentDefeatStateWithoutMutatingTheTarget()
        {
            AssertRejected(Payload(writer =>
            {
                writer.Write(1);
                WriteState(writer, Player, 0, 10, false, 1UL);
            }));
        }

        [Test]
        public void RejectsTruncatedAndTrailingPayloadsWithoutMutatingTheTarget()
        {
            byte[] valid = Payload(writer =>
            {
                writer.Write(1);
                WriteState(writer, Player, 7, 10, false, 1UL);
            });
            var truncated = new byte[valid.Length - 1];
            Array.Copy(valid, truncated, truncated.Length);
            AssertRejected(truncated);
            var trailing = new byte[valid.Length + 1];
            Array.Copy(valid, trailing, valid.Length);
            AssertRejected(trailing);
        }

        private static void AssertRejected(byte[] payload)
        {
            var target = new VitalityRegistry();
            VitalitySnapshot initial = VitalitySnapshot.Alive(Player, 10);
            Assert.That(target.Register(initial), Is.True);
            ISessionSnapshotContributor contributor = KentridgeVitalityPersistence.CreateContributor(
                () => target, () => Player);
            SessionContributorCapture valid = contributor.Capture(42UL);
            Assert.That(valid.Succeeded, Is.True, valid.Detail);
            var section = new SessionSectionSnapshot(
                contributor.SectionId, valid.Section.SemanticType, contributor.SchemaVersion, 42UL, payload);
            Assert.That(contributor.Validate(section).Succeeded, Is.False);
            Assert.That(contributor.Restore(section).Succeeded, Is.False);
            Assert.That(target.TryGet(Player, out VitalitySnapshot actual), Is.True);
            Assert.That(actual, Is.EqualTo(initial));
        }

        private static byte[] Payload(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                write(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void WriteState(BinaryWriter writer, CharacterId id, int current, int maximum,
            bool defeated, ulong revision)
        {
            writer.Write(id.Value);
            writer.Write(current);
            writer.Write(maximum);
            writer.Write(defeated);
            writer.Write(revision);
        }
    }
}
