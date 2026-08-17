using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseSecretAccessTests
    {
        [TestCase(GuildHouseKind.Assassins)]
        [TestCase(GuildHouseKind.Thieves)]
        public void SecretiveGuildsProduceConcealedPortalPlans(GuildHouseKind kind)
        {
            GuildHouseProgram program = GuildHouseProgramCatalog.Get(kind);
            GuildHouseSpatialPlan plan = GuildHouseSpatialPlanner.Plan(
                kind, 123u, int3.zero, 150, 140, program.PreferredRooms);
            GuildHouseSecretPortal[] portals = GuildHouseSecretAccessPlanner.Plan(in plan);

            Assert.That(portals.Length, Is.GreaterThan(0));
            for (int i = 0; i < portals.Length; i++)
            {
                Assert.That(portals[i].IsWellFormed, Is.True);
                Assert.That(plan.Rooms[portals[i].RoomIndex].Node.HiddenAccess, Is.True);
            }
        }

        [Test]
        public void WizardForbiddenArchiveGetsArcaneConcealedPortalWhenSelected()
        {
            GuildHouseProgram program = GuildHouseProgramCatalog.Get(GuildHouseKind.Wizards);
            GuildHouseSpatialPlan plan = GuildHouseSpatialPlanner.Plan(
                GuildHouseKind.Wizards, 456u, int3.zero, 150, 140, program.PreferredRooms);
            GuildHouseSecretPortal[] portals = GuildHouseSecretAccessPlanner.Plan(in plan);

            bool foundArcane = false;
            for (int i = 0; i < portals.Length; i++)
            {
                if (!portals[i].Arcane) continue;
                foundArcane = true;
                Assert.That(plan.Rooms[portals[i].RoomIndex].Node.Room.Role, Is.EqualTo(GuildHouseRoomRole.HiddenRoom));
            }
            Assert.That(foundArcane, Is.True);
        }

        [Test]
        public void SecretPortalPlanningIsDeterministic()
        {
            GuildHouseProgram program = GuildHouseProgramCatalog.Get(GuildHouseKind.Assassins);
            GuildHouseSpatialPlan a = GuildHouseSpatialPlanner.Plan(
                GuildHouseKind.Assassins, 789u, new int3(10, 3, 20), 150, 140, program.PreferredRooms);
            GuildHouseSpatialPlan b = GuildHouseSpatialPlanner.Plan(
                GuildHouseKind.Assassins, 789u, new int3(10, 3, 20), 150, 140, program.PreferredRooms);
            GuildHouseSecretPortal[] ap = GuildHouseSecretAccessPlanner.Plan(in a);
            GuildHouseSecretPortal[] bp = GuildHouseSecretAccessPlanner.Plan(in b);

            Assert.That(bp.Length, Is.EqualTo(ap.Length));
            for (int i = 0; i < ap.Length; i++)
            {
                Assert.That(bp[i].RoomIndex, Is.EqualTo(ap[i].RoomIndex));
                Assert.That(bp[i].Min, Is.EqualTo(ap[i].Min));
                Assert.That(bp[i].Size, Is.EqualTo(ap[i].Size));
                Assert.That(bp[i].Facing, Is.EqualTo(ap[i].Facing));
            }
        }
    }
}
