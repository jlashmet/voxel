using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatSetupV4Tests
    {
        [UnityTest]
        public IEnumerator SkitterCanStartAChainForAnotherPlayersReaction()
        {
            var board = new ChainCombatBoard();
            ChainUnitState skitter = Find(board, ChainRecruitKind.Skitter);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState goblinB = FindByName(board, "Goblin B");

            Assert.That(board.TryHarpoon(skitter.Id, goblinB.Id), Is.True, board.LastMessage);
            Assert.That(skitter.ActionSpent, Is.True);
            Assert.That(skitter.ReactionSpent, Is.False, "Starting a chain should not consume Skitter's separate reaction budget.");
            Assert.That(board.PendingReaction, Is.Not.Null);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.Collision));
            Assert.That(board.PendingReaction.PrimaryUnitId, Is.EqualTo(goblinB.Id));

            // P3 can now take possession of a chain P4 deliberately created.
            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.ClaimedByCommandGroup, Is.EqualTo(3));

            yield return null;
        }

        [UnityTest]
        public IEnumerator ConvergeActivelyBuildsFutureCollisionGeometry()
        {
            var board = new ChainCombatBoard();
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindByName(board, "Goblin A");

            int before = ChainCombatBoard.Distance(ogre.Position, goblinA.Position);
            Assert.That(before, Is.EqualTo(5));

            Assert.That(board.TryConverge(madeline.Id, ogre.Id, goblinA.Id), Is.True, board.LastMessage);

            int after = ChainCombatBoard.Distance(ogre.Position, goblinA.Position);
            Assert.That(after, Is.EqualTo(1), "Madeline should be able to spend a turn deliberately arranging the next player's collision play.");
            Assert.That(after, Is.LessThan(before));
            Assert.That(madeline.ActionSpent, Is.True);
            Assert.That(madeline.ReactionSpent, Is.False);
            Assert.That(board.PendingReaction, Is.Null, "This particular setup stops one cell short rather than gifting an automatic collision/reaction.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator CorrectlyUsingGromsNotchMakesTheLaterTreePayoffStronger()
        {
            var normal = new ChainCombatBoard();
            ChainUnitState normalOgre = Find(normal, ChainRecruitKind.Ogre);
            ChainUnitState normalGoblin = FindByName(normal, "Goblin A");
            RunCanonicalTreeChain(normal, notchFirst: false);
            int normalEnemyHp = normalOgre.Hp + normalGoblin.Hp;

            var prepared = new ChainCombatBoard();
            ChainUnitState preparedOgre = Find(prepared, ChainRecruitKind.Ogre);
            ChainUnitState preparedGoblin = FindByName(prepared, "Goblin A");
            RunCanonicalTreeChain(prepared, notchFirst: true);
            int preparedEnemyHp = preparedOgre.Hp + preparedGoblin.Hp;

            Assert.That(preparedEnemyHp, Is.LessThan(normalEnemyHp), "The team should get a real payoff for arranging the final fall along Grom's prepared direction.");
            Assert.That(FindTree(prepared, new GridPos(11, 4)).IsNotched, Is.True);
            Assert.That(Find(prepared, ChainRecruitKind.Grom).ActionSpent, Is.True, "Grom used a normal action to prepare the tree.");
            Assert.That(Find(prepared, ChainRecruitKind.Grom).ReactionSpent, Is.True, "Grom later spent his separate reaction to execute Timber.");

            yield return null;
        }

        private static void RunCanonicalTreeChain(ChainCombatBoard board, bool notchFirst)
        {
            ChainUnitState stephen = Find(board, ChainRecruitKind.Stephen);
            ChainUnitState weldon = Find(board, ChainRecruitKind.Weldon);
            ChainUnitState madeline = Find(board, ChainRecruitKind.Madeline);
            ChainUnitState grom = Find(board, ChainRecruitKind.Grom);
            ChainUnitState ogre = Find(board, ChainRecruitKind.Ogre);
            ChainUnitState goblinA = FindByName(board, "Goblin A");
            ChainTreeState tree = FindTree(board, new GridPos(11, 4));

            if (notchFirst)
            {
                Assert.That(board.TryNotchTree(grom.Id, tree.Id, new GridPos(0, 4)), Is.True, board.LastMessage);
                Assert.That(tree.NotchedDirection, Is.EqualTo(new GridPos(-1, 0)));
            }

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(weldon.Id, ChainReactionAbility.Crosswind), Is.True, board.LastMessage);
            Assert.That(board.TryCrosswind(weldon.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            Assert.That(board.TryClaimReaction(madeline.Id, ChainReactionAbility.Repulse), Is.True, board.LastMessage);
            Assert.That(board.TryRepulse(madeline.Id, goblinA.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(ChainReactionKind.TreeImpact));
            Assert.That(board.TryClaimReaction(grom.Id, ChainReactionAbility.Timber), Is.True, board.LastMessage);
            Assert.That(board.TryTimber(grom.Id, new GridPos(0, 4)), Is.True, board.LastMessage);
        }

        private static ChainUnitState Find(ChainCombatBoard board, ChainRecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Kind == kind) return board.Units[i];
            Assert.Fail($"Could not find unit kind {kind}.");
            return null;
        }

        private static ChainUnitState FindByName(ChainCombatBoard board, string name)
        {
            for (int i = 0; i < board.Units.Count; i++)
                if (board.Units[i].Name == name) return board.Units[i];
            Assert.Fail($"Could not find unit {name}.");
            return null;
        }

        private static ChainTreeState FindTree(ChainCombatBoard board, GridPos position)
        {
            ChainTreeState tree = board.FindStandingTreeAt(position);
            if (tree != null) return tree;

            for (int i = 0; i < board.Trees.Count; i++)
                if (board.Trees[i].Position.Equals(position)) return board.Trees[i];

            Assert.Fail($"Could not find tree at {position}.");
            return null;
        }
    }
}
