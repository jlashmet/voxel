using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePresentationPlanningTests
    {
        [Test]
        public void PresentationFollowsSpatialKeepTrapdoorAndBellTower()
        {
            CastlePlan plan = CastlePlanner.Create(new int3(720, 110, 940), 67u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(67u);
            topology.Perimeter = CastlePerimeterKind.IrregularQuadrilateral;
            topology.Wards = CastleWardPattern.SingleWard;
            topology.KeepPlacement = CastleKeepPlacement.Rear;
            topology.HasPosternGate = false;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleSpatialProjection projection = CastleSpatialProjection.Create(in plan, spatial);

            CastlePresentationLayout presentation = CastlePresentationPlanning.Create(in projection);

            Assert.AreEqual(23, presentation.Lights.Length);
            Assert.AreEqual(23, presentation.LightColours.Length);

            int2 keepCentre = projection.KeepCentreWorld;
            int baseY = projection.KeepPlan.Centre.y + projection.KeepPlan.PlateauHeight;
            Assert.AreEqual((keepCentre.x - 45) * 0.1f, presentation.Lights[0].x, 0.0001f);
            Assert.AreEqual((keepCentre.y - 28) * 0.1f, presentation.Lights[0].z, 0.0001f);

            Assert.AreEqual(projection.TrapdoorCentre.z * 0.1f,
                            presentation.Lights[10].z, 0.0001f,
                            "Dungeon presentation must move with the projected trapdoor/keep.");
            Assert.AreEqual(projection.ChapelBellTowerCentre.x * 0.1f,
                            presentation.Lights[20].x, 0.0001f);
            Assert.AreEqual(projection.ChapelBellTowerCentre.z * 0.1f,
                            presentation.Lights[20].z, 0.0001f);
            Assert.AreEqual((baseY + 17) * 0.1f, presentation.Lights[20].y, 0.0001f);
        }
    }
}
