using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatReservationV6Tests
    {
        [UnityTest]
        public IEnumerator PlayerCanReserveBeforeKnowingWhetherTheirRosterHasAnAnswer()
        {
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Airborne));

            // P3 is allowed to say "I've got this" without the system proving that P3 has a valid answer.
            Assert.That(reservations.TryReserve(3), Is.True, reservations.LastMessage);
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(3));

            // Madeline's actual capability does not answer Airborne. The failed attempt must not reveal alternatives
            // or silently give the event away.
            Assert.That(reservations.TryClaim(madeline.Id, ChainReactionAbility.Repulse), Is.False);
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(3));
            Assert.That(board.PendingReaction.IsClaimed, Is.False);

            Assert.That(reservations.TryReserve(2), Is.False, "Another player cannot steal an already reserved event.");
            Assert.That(reservations.TryReleaseReservation(3), Is.True, reservations.LastMessage);
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(0));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleasingConcreteChoiceKeepsPlayerReservation()
        {
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState brutus = Find(board, ChainRecruitKind.Brutus);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(reservations.TryReserve(2), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryClaim(weldon.Id, ChainReactionAbility.Crosswind), Is.True, reservations.LastMessage);
            Assert.That(board.PendingReaction.IsClaimed, Is.True);

            Assert.That(reservations.TryReleaseClaim(weldon.Id), Is.True, reservations.LastMessage);
            Assert.That(board.PendingReaction.IsClaimed, Is.False);
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(2),
                "Changing the recruit/ability choice should not reopen the event to click-racing players.");

            // Brutus is physically capable of answering this Airborne event, but P1 does not own the reservation.
            Assert.That(reservations.TryClaim(brutus.Id, ChainReactionAbility.CatchThrow), Is.False);
            Assert.That(board.PendingReaction.IsClaimed, Is.False);

            Assert.That(reservations.TryReleaseReservation(2), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryReserve(1), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryClaim(brutus.Id, ChainReactionAbility.CatchThrow), Is.True, reservations.LastMessage);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ExecutingReservedReactionHandsNextEventBackToWholeParty()
        {
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            int airborneEvent = board.PendingReaction.Id;

            Assert.That(reservations.TryReserve(2), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryClaim(weldon.Id, ChainReactionAbility.Crosswind), Is.True, reservations.LastMessage);
            Assert.That(board.TryCrosswind(weldon.Id, new GridPos(13, 4)), Is.True, board.LastMessage);

            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Id, Is.Not.EqualTo(airborneEvent));
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision));
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(0),
                "A reservation owns one physical decision, not the whole future cascade.");

            // The newly created collision can now be reserved by a different player.
            Assert.That(reservations.TryReserve(3), Is.True, reservations.LastMessage);
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(3));

            yield return null;
        }

        [UnityTest]
        public IEnumerator GroupPassCannotEraseAPlayersReservation()
        {
            var board = new ChainCombatBoard();
            var reservations = new ChainReactionReservationCoordinator(board);
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(reservations.TryReserve(2), Is.True, reservations.LastMessage);

            int eventId = board.PendingReaction.Id;
            Assert.That(reservations.TryPass(), Is.False, "A global pass must not steal a decision another player reserved.");
            Assert.That(board.PendingReaction.Id, Is.EqualTo(eventId));
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(2));

            Assert.That(reservations.TryReleaseReservation(2), Is.True, reservations.LastMessage);
            Assert.That(reservations.TryPass(), Is.True, reservations.LastMessage);
            Assert.That(board.PendingReaction, Is.Not.Null,
                "Passing the initial airborne window should let motion continue into the prepared collision.");
            Assert.That(reservations.ReservedByCommandGroup, Is.EqualTo(0));

            yield return null;
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Kind == kind) return board.Units[i];
            Assert.Fail($"Could not find unit kind {kind}.");
            return null;
        }
    }
}
