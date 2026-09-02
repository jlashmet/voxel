using NUnit.Framework;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Tests.EditMode
{
    /// <summary>
    /// Lossless transfer of the surface rules to the GPU.
    ///
    /// A GPU mesher that reads different rules from the CPU one produces a different surface for
    /// the same voxels, and the oracle test meant to catch that is only meaningful if both sides
    /// read identical rules. A field silently truncated in packing would pass every structural test
    /// and show up as a subtly wrong surface much later.
    /// </summary>
    public sealed class GpuSurfaceCataloguePackingTests
    {
        [Test]
        public void StyleSurvivesTheRoundTrip()
        {
            var style = new SurfaceStyleReadDefinition
            {
                StableId = 17,
                Reconstruction = SurfaceReconstruction.Sharp,
                Curvature = 200,
                JoinGroup = 9,
                PreserveSharpFeatures = true,
            };

            SurfaceStyleReadDefinition restored = GpuSurfaceCataloguePacking.UnpackStyle(
                GpuSurfaceCataloguePacking.PackStyle(style), style.StableId);

            Assert.AreEqual(style.Reconstruction, restored.Reconstruction);
            Assert.AreEqual(style.Curvature, restored.Curvature);
            Assert.AreEqual(style.JoinGroup, restored.JoinGroup);
            Assert.AreEqual(style.PreserveSharpFeatures, restored.PreserveSharpFeatures);
            Assert.AreEqual(style.StableId, restored.StableId);
        }

        [Test]
        public void EveryReconstructionModeIsDistinguishable()
        {
            // Reconstruction decides whether a cell is meshed smooth, planar, sharp or cubic.
            // Collapsing two of them onto the same code would silently round off architecture.
            foreach (SurfaceReconstruction mode in
                     System.Enum.GetValues(typeof(SurfaceReconstruction)))
            {
                var style = new SurfaceStyleReadDefinition { Reconstruction = mode };
                Assert.AreEqual(mode,
                    GpuSurfaceCataloguePacking.UnpackStyle(
                        GpuSurfaceCataloguePacking.PackStyle(style), 0).Reconstruction,
                    $"{mode} did not survive packing");
            }
        }

        [Test]
        public void CurvatureKeepsItsFullByteRange()
        {
            for (int curvature = 0; curvature <= 255; curvature++)
            {
                var style = new SurfaceStyleReadDefinition { Curvature = (byte)curvature };
                Assert.AreEqual(curvature,
                    GpuSurfaceCataloguePacking.UnpackStyle(
                        GpuSurfaceCataloguePacking.PackStyle(style), 0).Curvature);
            }
        }

        [Test]
        public void JoinRuleSurvivesTheRoundTrip()
        {
            var join = new SurfaceJoinReadRule
            {
                Compatibility = SurfaceCompatibility.Reject,
                Continuity = SurfaceContinuity.Discontinuous,
                BlendWidth = 250,
                DominantGroup = 15,
                TransitionStyleId = 2000,
                PreserveSharpFeature = true,
            };

            SurfaceJoinReadRule restored =
                GpuSurfaceCataloguePacking.UnpackJoin(GpuSurfaceCataloguePacking.PackJoin(join));

            Assert.AreEqual(join.Compatibility, restored.Compatibility);
            Assert.AreEqual(join.Continuity, restored.Continuity);
            Assert.AreEqual(join.BlendWidth, restored.BlendWidth);
            Assert.AreEqual(join.DominantGroup, restored.DominantGroup);
            Assert.AreEqual(join.TransitionStyleId, restored.TransitionStyleId);
            Assert.AreEqual(join.PreserveSharpFeature, restored.PreserveSharpFeature);
        }

        [Test]
        public void CoatingSurvivesTheRoundTripIncludingItsMaterialMask()
        {
            var coating = new CoatingReadDefinition
            {
                StableId = 3,
                AllowedMaterialMask = 0xDEADBEEF,
                Displacement = 200,
                DecorationShape = SurfaceDecorationShape.Clump,
                DecorationDensity = 128,
                DecorationRadiusQ4 = 240,
                DecorationHeightQ4 = 33,
                DecorationDropQ4 = 44,
                DecorationSeparation = 55,
                DecorationFaceMask = 63,
            };

            GpuSurfaceCataloguePacking.PackCoating(coating, out uint w0, out uint w1, out uint w2);
            CoatingReadDefinition restored =
                GpuSurfaceCataloguePacking.UnpackCoating(w0, w1, w2, coating.StableId);

            Assert.AreEqual(coating.AllowedMaterialMask, restored.AllowedMaterialMask,
                "The allowed-material mask needs a full word of its own; folding it in with the "
              + "byte fields would drop coatings from materials that should carry them.");
            Assert.AreEqual(coating.Displacement, restored.Displacement);
            Assert.AreEqual(coating.DecorationShape, restored.DecorationShape);
            Assert.AreEqual(coating.DecorationDensity, restored.DecorationDensity);
            Assert.AreEqual(coating.DecorationRadiusQ4, restored.DecorationRadiusQ4);
            Assert.AreEqual(coating.DecorationHeightQ4, restored.DecorationHeightQ4);
            Assert.AreEqual(coating.DecorationDropQ4, restored.DecorationDropQ4);
            Assert.AreEqual(coating.DecorationSeparation, restored.DecorationSeparation);
            Assert.AreEqual(coating.DecorationFaceMask, restored.DecorationFaceMask);
        }

        [Test]
        public void JoinIndexIsDirectionalAndCoversEveryGroupPair()
        {
            Assert.AreNotEqual(GpuSurfaceCataloguePacking.JoinIndex(1, 2),
                               GpuSurfaceCataloguePacking.JoinIndex(2, 1),
                "Join rules are looked up per ordered pair; collapsing them would lose the "
              + "dominant side of an asymmetric seam.");

            var seen = new System.Collections.Generic.HashSet<int>();
            for (byte a = 0; a < GpuSurfaceCataloguePacking.JoinGroupCount; a++)
            for (byte b = 0; b < GpuSurfaceCataloguePacking.JoinGroupCount; b++)
                Assert.IsTrue(seen.Add(GpuSurfaceCataloguePacking.JoinIndex(a, b)));

            Assert.AreEqual(GpuSurfaceCataloguePacking.JoinRuleCount, seen.Count);
        }

        [Test]
        public void AWholeCataloguePacksWithoutOverrunningItsBuffers()
        {
            // A real catalogue, not default(): an uncaptured view has empty join storage, and
            // production always binds a captured or built-in one. Packing is strict about that on
            // purpose — an empty catalogue is a configuration fault worth surfacing, not padding.
            SurfaceCatalogueView catalogue = SurfaceCatalogueView.CreateBuiltIns();
            var styleWords = new uint[GpuSurfaceCataloguePacking.StyleCount];
            var joinWords = new uint[GpuSurfaceCataloguePacking.JoinRuleCount];

            Assert.DoesNotThrow(() =>
                GpuSurfaceCataloguePacking.PackCatalogue(catalogue, styleWords, joinWords));
        }

        [Test]
        public void AWholeCoatingCatalogueFitsItsBuffer()
        {
            CoatingCatalogueView coatings = default;
            var words = new uint[GpuSurfaceCataloguePacking.CoatingCount
                               * GpuSurfaceCataloguePacking.CoatingWords];

            Assert.DoesNotThrow(() => GpuSurfaceCataloguePacking.PackCoatings(coatings, words));
        }
    }
}
