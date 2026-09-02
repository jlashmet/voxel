using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class CastleCaveAuthoringBoundaryTests
    {
        [Test]
        public void CastleAdapterDelegatesSemanticRequestConfigAndPaletteExactlyOnce()
        {
            var recorder = new RecordingCaveAuthoring();
            var plan = new CastlePlan { Seed = 0x13579BDFu };
            var at = new int3(120, -64, 345);

            CaveGenerationRequest expectedRequest = CastleCaveAuthoring.Request(in plan, at);
            CaveConfig expectedConfig = CastleCaveAuthoring.CompatibilityConfig;
            CaveMaterialPalette expectedPalette = CastleCaveAuthoring.CompatibilityPalette;

            CastleCaveAuthoring.Author(recorder, null, in plan, at);

            Assert.That(recorder.CallCount, Is.EqualTo(1));
            Assert.That(recorder.Request, Is.EqualTo(expectedRequest));
            Assert.That(recorder.Config, Is.EqualTo(expectedConfig));
            Assert.That(recorder.Palette, Is.EqualTo(expectedPalette));
            Assert.That(recorder.Request, Is.EqualTo(CastleCaveAuthoring.Request(in plan, at)),
                "Fixed castle seed/anchor inputs must remain deterministic.");
        }

        private sealed class RecordingCaveAuthoring : ICaveAuthoring
        {
            public int CallCount { get; private set; }
            public CaveGenerationRequest Request { get; private set; }
            public CaveConfig Config { get; private set; }
            public CaveMaterialPalette Palette { get; private set; }

            public CaveAuthoringResult Author(
                IStructureAuthoringSession authoring,
                in CaveGenerationRequest request,
                in CaveConfig config,
                in CaveMaterialPalette palette)
            {
                CallCount++;
                Request = request;
                Config = config;
                Palette = palette;
                return default;
            }
        }
    }
}
