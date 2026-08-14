using System.Collections;
using MountingForce.CombatPrototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class ChainCombatPrototypeTests
    {
        [UnityTest]
        public IEnumerator ControllerBootsPlayableBattlePresentation()
        {
            var root = new GameObject("Chain Combat Prototype Test Root");
            root.AddComponent<CombatPrototypeController>();

            yield return null;

            Assert.That(GameObject.Find("Combat Prototype Camera"), Is.Not.Null, "Prototype camera was not created.");
            Assert.That(GameObject.Find("Combat Prototype Light"), Is.Not.Null, "Prototype light was not created.");
            Assert.That(GameObject.Find("Combat Prototype Visuals"), Is.Not.Null, "Prototype visual root was not created.");
            Assert.That(GameObject.Find("Unit - Stephen"), Is.Not.Null, "Stephen visual was not created.");
            Assert.That(GameObject.Find("Unit - Mira"), Is.Not.Null, "Mira visual was not created.");
            Assert.That(GameObject.Find("Unit - Weldon"), Is.Not.Null, "Weldon visual was not created.");
            Assert.That(GameObject.Find("Unit - Madeline"), Is.Not.Null, "Madeline visual was not created.");
            Assert.That(GameObject.Find("Unit - Grom"), Is.Not.Null, "Grom visual was not created.");
            Assert.That(GameObject.Find("Unit - Ogre"), Is.Not.Null, "Ogre visual was not created.");
            Assert.That(GameObject.Find("Unit - Goblin A"), Is.Not.Null, "Goblin A visual was not created.");
            Assert.That(GameObject.Find("Unit - Goblin B"), Is.Not.Null, "Goblin B visual was not created.");
            Assert.That(GameObject.Find("Tree 1"), Is.Not.Null, "First tree visual was not created.");
            Assert.That(GameObject.Find("Tree 2"), Is.Not.Null, "Second tree visual was not created.");
            Assert.That(GameObject.Find("Cell 0,0"), Is.Not.Null, "Battle grid origin was not created.");
            Assert.That(GameObject.Find("Cell 13,9"), Is.Not.Null, "Battle grid far corner was not created.");

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReactionChainRunsLaunchRedirectCollisionRepulseAndTreeFall()
        {
            var board = new CombatBoard();
            UnitState stephen = Find(board, RecruitKind.Stephen);
            UnitState weldon = Find(board, RecruitKind.Weldon);
            UnitState madeline = Find(board, RecruitKind.Madeline);
            UnitState grom = Find(board, RecruitKind.Grom);
            UnitState ogre = Find(board, RecruitKind.Ogre);
            UnitState goblinA = FindByName(board, "Goblin A");
            TreeState tree = board.FindStandingTreeAt(new GridPos(11, 4));

            Assert.That(board.Units.Count, Is.EqualTo(8));
            Assert.That(board.Trees.Count, Is.EqualTo(2));
            Assert.That(tree, Is.Not.Null);

            Assert.That(board.TryUppercut(stephen.Id, ogre.Id), Is.True, board.LastMessage);
            AssertReaction(board, ReactionKind.Airborne, ogre.Id);
            Assert.That(ogre.Airborne, Is.True);
            yield return null;

            Assert.That(board.TryCrosswind(weldon.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            AssertReaction(board, ReactionKind.Collision, ogre.Id);
            Assert.That(board.PendingReaction.SecondaryUnitId, Is.EqualTo(goblinA.Id));
            Assert.That(ogre.Position, Is.EqualTo(new GridPos(7, 4)));
            Assert.That(goblinA.Position, Is.EqualTo(new GridPos(8, 4)));
            Assert.That(ogre.Hp, Is.EqualTo(6));
            Assert.That(goblinA.Hp, Is.EqualTo(3));
            yield return null;

            Assert.That(board.TryRepulse(madeline.Id, goblinA.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            AssertReaction(board, ReactionKind.TreeImpact, goblinA.Id);
            Assert.That(board.PendingReaction.TreeId, Is.EqualTo(tree.Id));
            Assert.That(goblinA.Position, Is.EqualTo(new GridPos(10, 4)));
            Assert.That(goblinA.Hp, Is.EqualTo(2));
            yield return null;

            Assert.That(board.TryTimber(grom.Id, new GridPos(13, 4)), Is.True, board.LastMessage);
            Assert.That(board.PendingReaction, Is.Null);
            Assert.That(tree.Standing, Is.False);
            Assert.That(tree.FallDirection, Is.EqualTo(new GridPos(1, 0)));
            Assert.That(stephen.ActionSpent, Is.True);
            Assert.That(weldon.ReactionSpent, Is.True);
            Assert.That(madeline.ReactionSpent, Is.True);
            Assert.That(grom.ReactionSpent, Is.True);
            Assert.That(LogContains(board, "uppercut"), Is.True);
            Assert.That(LogContains(board, "redirected"), Is.True);
            Assert.That(LogContains(board, "collided"), Is.True);
            Assert.That(LogContains(board, "blasted"), Is.True);
            Assert.That(LogContains(board, "falling"), Is.True);
            yield return null;

            Assert.That(board.EndRound(), Is.True, board.LastMessage);
            Assert.That(board.Round, Is.EqualTo(2));
            Assert.That(stephen.ActionSpent, Is.False, "Friendly action budget should refresh at round start.");
            Assert.That(weldon.ReactionSpent, Is.False, "Friendly reaction budget should refresh at round start.");
            Assert.That(madeline.ReactionSpent, Is.False, "Friendly reaction budget should refresh at round start.");
            Assert.That(grom.ReactionSpent, Is.False, "Friendly reaction budget should refresh at round start.");
        }

        [UnityTest]
        public IEnumerator PortalsPreserveMotionAndForceMultiplierExtendsMotion()
        {
            var portalBoard = new CombatBoard();
            UnitState portalMira = Find(portalBoard, RecruitKind.Mira);
            UnitState portalStephen = Find(portalBoard, RecruitKind.Stephen);
            UnitState portalOgre = Find(portalBoard, RecruitKind.Ogre);

            Assert.That(
                portalBoard.TryPlacePortalPair(portalMira.Id, new GridPos(4, 4), new GridPos(4, 1)),
                Is.True,
                portalBoard.LastMessage);
            Assert.That(portalBoard.PortalA, Is.EqualTo(new GridPos(4, 4)));
            Assert.That(portalBoard.PortalB, Is.EqualTo(new GridPos(4, 1)));

            Assert.That(portalBoard.TryUppercut(portalStephen.Id, portalOgre.Id), Is.True, portalBoard.LastMessage);
            Assert.That(portalBoard.PassReaction(), Is.True, portalBoard.LastMessage);
            Assert.That(portalBoard.PendingReaction, Is.Null);
            Assert.That(portalOgre.Position, Is.EqualTo(new GridPos(8, 1)), "Portal traversal should preserve eastward direction and remaining force.");
            Assert.That(portalOgre.Airborne, Is.False);
            yield return null;

            var amplifierBoard = new CombatBoard();
            UnitState amplifierMira = Find(amplifierBoard, RecruitKind.Mira);
            UnitState amplifierStephen = Find(amplifierBoard, RecruitKind.Stephen);
            UnitState amplifierOgre = Find(amplifierBoard, RecruitKind.Ogre);
            UnitState amplifierGoblin = FindByName(amplifierBoard, "Goblin A");

            Assert.That(amplifierBoard.TryPlaceAmplifier(amplifierMira.Id, new GridPos(4, 4)), Is.True, amplifierBoard.LastMessage);
            Assert.That(amplifierBoard.Amplifiers, Does.Contain(new GridPos(4, 4)));
            Assert.That(amplifierBoard.TryUppercut(amplifierStephen.Id, amplifierOgre.Id), Is.True, amplifierBoard.LastMessage);
            Assert.That(amplifierBoard.PassReaction(), Is.True, amplifierBoard.LastMessage);

            AssertReaction(amplifierBoard, ReactionKind.Collision, amplifierOgre.Id);
            Assert.That(amplifierBoard.PendingReaction.SecondaryUnitId, Is.EqualTo(amplifierGoblin.Id));
            Assert.That(LogContains(amplifierBoard, "remaining momentum 4 -> 8"), Is.True, "Force multiplier did not increase remaining momentum as expected.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NormalActionsActionBudgetAndResetWork()
        {
            var moveBoard = new CombatBoard();
            UnitState mira = Find(moveBoard, RecruitKind.Mira);

            Assert.That(moveBoard.TryMove(mira.Id, new GridPos(1, 3)), Is.True, moveBoard.LastMessage);
            Assert.That(mira.Position, Is.EqualTo(new GridPos(1, 3)));
            Assert.That(mira.ActionSpent, Is.True);
            Assert.That(moveBoard.TryMove(mira.Id, new GridPos(1, 4)), Is.False, "A recruit should not get two normal actions in one round.");
            yield return null;

            var strikeBoard = new CombatBoard();
            UnitState stephen = Find(strikeBoard, RecruitKind.Stephen);
            UnitState ogre = Find(strikeBoard, RecruitKind.Ogre);

            Assert.That(strikeBoard.TryBasicHit(stephen.Id, ogre.Id), Is.True, strikeBoard.LastMessage);
            Assert.That(ogre.Hp, Is.EqualTo(6));
            Assert.That(stephen.ActionSpent, Is.True);
            Assert.That(strikeBoard.TryUppercut(stephen.Id, ogre.Id), Is.False, "Stephen should not strike and uppercut in the same round.");

            Assert.That(strikeBoard.EndRound(), Is.True, strikeBoard.LastMessage);
            Assert.That(strikeBoard.Round, Is.EqualTo(2));
            Assert.That(stephen.ActionSpent, Is.False);
            yield return null;

            strikeBoard.Reset();
            UnitState resetOgre = Find(strikeBoard, RecruitKind.Ogre);
            Assert.That(strikeBoard.Round, Is.EqualTo(1));
            Assert.That(resetOgre.Hp, Is.EqualTo(resetOgre.MaxHp));
            Assert.That(strikeBoard.PendingReaction, Is.Null);
            Assert.That(strikeBoard.PortalA, Is.Null);
            Assert.That(strikeBoard.PortalB, Is.Null);
            Assert.That(strikeBoard.Amplifiers.Count, Is.EqualTo(0));
        }

        private static UnitState Find(CombatBoard board, RecruitKind kind)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                if (board.Units[i].Kind == kind)
                {
                    return board.Units[i];
                }
            }

            Assert.Fail($"Could not find unit of kind {kind}.");
            return null;
        }

        private static UnitState FindByName(CombatBoard board, string name)
        {
            for (int i = 0; i < board.Units.Count; i++)
            {
                if (board.Units[i].Name == name)
                {
                    return board.Units[i];
                }
            }

            Assert.Fail($"Could not find unit named {name}.");
            return null;
        }

        private static void AssertReaction(CombatBoard board, ReactionKind kind, int primaryUnitId)
        {
            Assert.That(board.PendingReaction, Is.Not.Null, "Expected a reaction window.");
            Assert.That(board.PendingReaction.Kind, Is.EqualTo(kind));
            Assert.That(board.PendingReaction.PrimaryUnitId, Is.EqualTo(primaryUnitId));
        }

        private static bool LogContains(CombatBoard board, string fragment)
        {
            for (int i = 0; i < board.Log.Count; i++)
            {
                if (board.Log[i].Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
