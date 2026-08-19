using VoxelEngine.Storage.Api;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>
    /// The surface, join and coating rules as 32-bit words a compute shader can index.
    ///
    /// Density sampling is not a threshold on occupancy — it consults the style's reconstruction
    /// mode, its curvature, the coating's displacement, and the join rule between two adjacent
    /// styles. Those rules are what spec 003 added, and a GPU mesher that ignores them produces a
    /// different surface from the CPU one for the same voxels. So they have to cross to the GPU
    /// before any meshing can, and they have to cross losslessly: the oracle test that proves the
    /// two meshers agree is only meaningful if both read identical rules.
    ///
    /// The catalogues are bounded and tiny — 32 styles, 256 join rules, 16 coatings — so they are
    /// packed whole and re-uploaded on a version change rather than diffed.
    ///
    /// Packing lives apart from the buffers so the bit layout can be round-tripped in a test
    /// without a graphics device. A silently truncated field here would show up as a subtly wrong
    /// surface much later, which is the expensive way to find it.
    /// </summary>
    public static class GpuSurfaceCataloguePacking
    {
        public const int StyleWords = 1;
        public const int JoinWords = 1;
        public const int CoatingWords = 3;

        public const int StyleCount = SurfaceCatalogueView.MaxStyles;
        public const int JoinGroupCount = SurfaceCatalogueView.MaxJoinGroups;
        public const int JoinRuleCount = JoinGroupCount * JoinGroupCount;
        public const int CoatingCount = CoatingCatalogueView.MaxCoatings;

        public static int JoinIndex(byte groupA, byte groupB) => groupA * JoinGroupCount + groupB;

        public static uint PackStyle(in SurfaceStyleReadDefinition style) =>
            (uint)style.Reconstruction
            | ((uint)style.Curvature << 8)
            | ((uint)style.JoinGroup << 16)
            | (style.PreserveSharpFeatures ? 1u << 24 : 0u);

        public static SurfaceStyleReadDefinition UnpackStyle(uint packed, ushort stableId) => new()
        {
            StableId = stableId,
            Reconstruction = (SurfaceReconstruction)(packed & 0xFFu),
            Curvature = (byte)((packed >> 8) & 0xFFu),
            JoinGroup = (byte)((packed >> 16) & 0xFFu),
            PreserveSharpFeatures = (packed & (1u << 24)) != 0,
        };

        public static uint PackJoin(in SurfaceJoinReadRule join) =>
            (uint)join.Compatibility
            | ((uint)join.Continuity << 4)
            | ((uint)join.BlendWidth << 8)
            | ((uint)join.DominantGroup << 16)
            | ((uint)join.TransitionStyleId << 20)
            | (join.PreserveSharpFeature ? 1u << 31 : 0u);

        public static SurfaceJoinReadRule UnpackJoin(uint packed) => new()
        {
            Compatibility = (SurfaceCompatibility)(packed & 0xFu),
            Continuity = (SurfaceContinuity)((packed >> 4) & 0xFu),
            BlendWidth = (byte)((packed >> 8) & 0xFFu),
            DominantGroup = (byte)((packed >> 16) & 0xFu),
            TransitionStyleId = (ushort)((packed >> 20) & 0x7FFu),
            PreserveSharpFeature = (packed & (1u << 31)) != 0,
        };

        public static void PackCoating(in CoatingReadDefinition coating,
                                       out uint word0, out uint word1, out uint word2)
        {
            word0 = coating.AllowedMaterialMask;
            word1 = coating.Displacement
                  | ((uint)coating.DecorationShape << 8)
                  | ((uint)coating.DecorationDensity << 16)
                  | ((uint)coating.DecorationRadiusQ4 << 24);
            word2 = coating.DecorationHeightQ4
                  | ((uint)coating.DecorationDropQ4 << 8)
                  | ((uint)coating.DecorationSeparation << 16)
                  | ((uint)coating.DecorationFaceMask << 24);
        }

        public static CoatingReadDefinition UnpackCoating(uint word0, uint word1, uint word2,
                                                          byte stableId) => new()
        {
            StableId = stableId,
            AllowedMaterialMask = word0,
            Displacement = (byte)(word1 & 0xFFu),
            DecorationShape = (SurfaceDecorationShape)((word1 >> 8) & 0xFFu),
            DecorationDensity = (byte)((word1 >> 16) & 0xFFu),
            DecorationRadiusQ4 = (byte)((word1 >> 24) & 0xFFu),
            DecorationHeightQ4 = (byte)(word2 & 0xFFu),
            DecorationDropQ4 = (byte)((word2 >> 8) & 0xFFu),
            DecorationSeparation = (byte)((word2 >> 16) & 0xFFu),
            DecorationFaceMask = (byte)((word2 >> 24) & 0xFFu),
        };

        /// <summary>Packs an entire surface catalogue into caller-owned word arrays.</summary>
        public static void PackCatalogue(in SurfaceCatalogueView catalogue,
                                         uint[] styleWords, uint[] joinWords)
        {
            for (int style = 0; style < StyleCount; style++)
                styleWords[style] = PackStyle(catalogue.Get((ushort)style));

            for (byte a = 0; a < JoinGroupCount; a++)
            for (byte b = 0; b < JoinGroupCount; b++)
                joinWords[JoinIndex(a, b)] = PackJoin(catalogue.GetJoin(a, b));
        }

        public static void PackCoatings(in CoatingCatalogueView coatings, uint[] words)
        {
            for (int coating = 0; coating < CoatingCount; coating++)
            {
                PackCoating(coatings.Get((byte)coating),
                            out uint w0, out uint w1, out uint w2);
                words[coating * CoatingWords + 0] = w0;
                words[coating * CoatingWords + 1] = w1;
                words[coating * CoatingWords + 2] = w2;
            }
        }
    }
}
