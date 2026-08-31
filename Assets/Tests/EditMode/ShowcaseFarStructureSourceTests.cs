using System.Collections.Generic;
using Game.WorldBuilder.Api;
using MountingForce.WorldGen.Architecture;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class ShowcaseFarStructureSourceTests
    {
        [Test]
        public void Query_UsesSemanticSourceWithoutResidencyAndPreservesStableIdentity()
        {
            var record = new StructureFarPresentation(
                0x1234UL,
                0x55UL,
                new Int2(100, 200),
                new Int2(300, 500),
                120,
                (FrontageDirection)0,
                (StructureArchetype)0,
                0x77UL,
                0x8899UL,
                StructureVisibilityClass.Landmark,
                0xABCDUL);
            var source = new FakeVisibilitySource(record);
            float2 policyCamera = new float2(-1f, -1f);
            var adapter = new ShowcaseFarStructureSource(
                source,
                (_, camera) =>
                {
                    policyCamera = camera;
                    return FarStructureTier.Far;
                },
                xz => xz.x + xz.y);

            var cameraXZ = new float2(20f, 35f);
            IReadOnlyList<FarStructureInstance> instances = adapter.Query(cameraXZ, 30f);

            Assert.That(source.QueryCount, Is.EqualTo(1));
            Assert.That(policyCamera.x, Is.EqualTo(cameraXZ.x));
            Assert.That(policyCamera.y, Is.EqualTo(cameraXZ.y));
            Assert.That(instances, Has.Count.EqualTo(1));
            FarStructureInstance instance = instances[0];
            Assert.That(instance.StableId, Is.EqualTo(0x1234UL));
            Assert.That(instance.Tier, Is.EqualTo(FarStructureTier.Far));
            Assert.That(instance.Position.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(instance.Position.z, Is.EqualTo(35f).Within(0.001f));
            Assert.That(instance.Position.y, Is.EqualTo(55f).Within(0.001f));
            Assert.That(instance.Scale.x, Is.EqualTo(20f).Within(0.001f));
            Assert.That(instance.Scale.y, Is.EqualTo(12f).Within(0.001f));
            Assert.That(instance.Scale.z, Is.EqualTo(30f).Within(0.001f));
            Assert.That((instance.Flags & FarStructureVisualFlags.Landmark) != 0, Is.True);
        }

        [Test]
        public void Query_CulledPolicyOmitsRecord()
        {
            var record = new StructureFarPresentation(
                1UL,
                2UL,
                new Int2(0, 0),
                new Int2(100, 100),
                50,
                (FrontageDirection)0,
                (StructureArchetype)0,
                3UL,
                4UL,
                StructureVisibilityClass.OrdinaryStructure,
                5UL);
            var adapter = new ShowcaseFarStructureSource(
                new FakeVisibilitySource(record),
                (_, __) => FarStructureTier.Culled,
                _ => 0f);

            Assert.That(adapter.Query(float2.zero, 20f), Is.Empty);
        }

        private sealed class FakeVisibilitySource : IWorldVisibilitySource
        {
            private readonly StructureFarPresentation _record;

            public FakeVisibilitySource(StructureFarPresentation record)
            {
                _record = record;
            }

            public int QueryCount { get; private set; }

            public bool TryGet(ulong structureKey, out StructureFarPresentation value)
            {
                value = _record;
                return structureKey == _record.StructureKey;
            }

            public IReadOnlyList<StructureFarPresentation> Query(WorldVisibilityBoundsDm bounds)
            {
                QueryCount++;
                return bounds.Intersects(_record)
                    ? new[] { _record }
                    : System.Array.Empty<StructureFarPresentation>();
            }
        }
    }
}
