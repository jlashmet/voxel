using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Structures.Tests
{
    public sealed class DecorationThinSurfaceBatchTests
    {
        [Test]
        public void BedroomThinSurfacesBecomeAggregatedSubVoxelQuads()
        {
            DecorationSpace space = BedroomSpace();
            DecorationContext context = Context();
            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in context, null, out DecorationPlacement[] placements));

            const float voxelSize = 0.1f;
            const float surfaceOffset = 0.005f;
            Assert.IsTrue(DecorationThinSurfaceBatchBuilder.TryBuild(
                placements, voxelSize, surfaceOffset, out DecorationThinSurfaceBatch batch));

            Assert.Multiple(() =>
            {
                Assert.IsTrue(batch.IsWellFormed);
                Assert.AreEqual(2, batch.SurfaceCount);
                Assert.AreEqual(8, batch.Vertices.Length);
                Assert.AreEqual(12, batch.Indices.Length);
                Assert.AreEqual(DecorationPropFamily.Rug, batch.Ranges[0].Family);
                Assert.AreEqual(DecorationPropFamily.Painting, batch.Ranges[1].Family);
                Assert.Less(surfaceOffset, voxelSize);
            });

            DecorationPlacement rug = placements[1];
            float rugY = rug.Bounds.Min.y * voxelSize + surfaceOffset;
            for (int i = 0; i < 4; i++)
            {
                Assert.That(batch.Vertices[i].Position.y, Is.EqualTo(rugY).Within(0.00001f));
                Assert.AreEqual(new float3(0f, 1f, 0f), batch.Vertices[i].Normal);
            }

            DecorationPlacement painting = placements[3];
            int paintingStart = batch.Ranges[1].VertexStart;
            if (math.abs(painting.Facing.x) == 1)
            {
                float plane = painting.Facing.x > 0
                    ? painting.Bounds.Min.x * voxelSize + surfaceOffset
                    : painting.Bounds.MaxExclusive.x * voxelSize - surfaceOffset;
                for (int i = 0; i < 4; i++)
                    Assert.That(batch.Vertices[paintingStart + i].Position.x,
                        Is.EqualTo(plane).Within(0.00001f));
            }
            else
            {
                float plane = painting.Facing.z > 0
                    ? painting.Bounds.Min.z * voxelSize + surfaceOffset
                    : painting.Bounds.MaxExclusive.z * voxelSize - surfaceOffset;
                for (int i = 0; i < 4; i++)
                    Assert.That(batch.Vertices[paintingStart + i].Position.z,
                        Is.EqualTo(plane).Within(0.00001f));
            }
        }

        [Test]
        public void ThinSurfaceBatchIsStableForIdenticalSemanticPlacements()
        {
            DecorationSpace space = BedroomSpace();
            DecorationContext context = Context();
            Assert.IsTrue(BedroomSceneResolver.TryResolve(
                in space, in context, null, out DecorationPlacement[] placements));

            Assert.IsTrue(DecorationThinSurfaceBatchBuilder.TryBuild(
                placements, 0.1f, 0.002f, out DecorationThinSurfaceBatch first));
            Assert.IsTrue(DecorationThinSurfaceBatchBuilder.TryBuild(
                placements, 0.1f, 0.002f, out DecorationThinSurfaceBatch second));

            Assert.AreEqual(first.SurfaceCount, second.SurfaceCount);
            for (int i = 0; i < first.Ranges.Length; i++)
            {
                Assert.AreEqual(first.Ranges[i].Id, second.Ranges[i].Id);
                Assert.AreEqual(first.Ranges[i].Family, second.Ranges[i].Family);
            }
            for (int i = 0; i < first.Vertices.Length; i++)
            {
                Assert.AreEqual(first.Vertices[i].Position, second.Vertices[i].Position);
                Assert.AreEqual(first.Vertices[i].Normal, second.Vertices[i].Normal);
                Assert.AreEqual(first.Vertices[i].Uv, second.Vertices[i].Uv);
            }
        }

        private static DecorationSpace BedroomSpace() => new DecorationSpace
        {
            SpaceId = 0xBED001u,
            Kind = DecorationSpaceKind.Bedroom,
            Bounds = new DecorationBounds
            {
                Min = new int3(-60, 10, -50),
                MaxExclusive = new int3(60, 58, 50),
            },
        };

        private static DecorationContext Context() => new DecorationContext
        {
            WorldSeed = 0xAABBCCDDu,
            StructureId = 0xCA571Eu,
            SpaceId = 0xBED001u,
            StyleId = DecorationStyleIds.Compose(DecorationStyleFamily.Courtly, 17u),
            StructureKind = DecorationStructureKind.Castle,
            SpaceKind = DecorationSpaceKind.Bedroom,
            Wealth = DecorationWealthTier.Wealthy,
            Condition = DecorationConditionTier.Maintained,
            Environment = DecorationEnvironmentTags.Interior | DecorationEnvironmentTags.Residential,
        };
    }
}
