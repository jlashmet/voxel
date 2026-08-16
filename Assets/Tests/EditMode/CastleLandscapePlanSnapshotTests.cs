using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastleLandscapePlanSnapshotTests
    {
        [Test]
        public void SnapshotIsDeepAndValidated()
        {
            CastlePlan plan = CastlePlanner.Create(int3.zero, 311u);
            CastleTopologyPlan topology = CastleLayoutPlanner.Create(311u);
            topology.KeepPlacement = CastleKeepPlacement.Central;
            CastleSpatialPlan spatial = CastleSpatialPlanner.Create(in plan, in topology);
            CastleApproachFrame approach = CastleApproachFrame.FromGate(in spatial.PrimaryGate);
            CastleLandscapePlan source = CastleLandscapePlanner.Create(
                in plan, spatial.OuterWardVertices, in approach);

            CastleLandscapePlan snapshot = CastleLandscapePlanSnapshot.CloneValidated(source);
            CastleLandscapeDecorationSpec expected = snapshot.Decorations[0];

            CastleLandscapeDecorationSpec changed = source.Decorations[0];
            changed.Centre += new int2(999, 999);
            changed.Radius += 50;
            source.Decorations[0] = changed;

            Assert.AreEqual(expected.Centre, snapshot.Decorations[0].Centre);
            Assert.AreEqual(expected.Radius, snapshot.Decorations[0].Radius);
            Assert.IsTrue(CastleLandscapePlanValidator.TryValidate(
                snapshot, out CastleLandscapePlanIssue issue), issue.ToString());
        }
    }
}
