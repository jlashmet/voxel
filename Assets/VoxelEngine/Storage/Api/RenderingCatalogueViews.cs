using Unity.Collections;

namespace VoxelEngine.Storage.Api
{
    public enum SurfaceReconstruction : byte
    {
        Smooth = 0,
        Planar = 1,
        Rounded = 2,
        Sharp = 3,
        Cubic = 4,
    }

    public enum SurfaceContinuity : byte
    {
        Discontinuous = 0,
        Tangent = 1,
        Smooth = 2,
    }

    public enum SurfaceCompatibility : byte
    {
        Join = 0,
        Seam = 1,
        Reject = 2,
    }

    public struct SurfaceStyleReadDefinition
    {
        public ushort StableId;
        public SurfaceReconstruction Reconstruction;
        public byte Curvature;
        public byte JoinGroup;
        public bool PreserveSharpFeatures;
    }

    public enum SurfaceDecorationShape : byte
    {
        None = 0,
        Clump = 1,
    }

    public struct CoatingReadDefinition
    {
        public byte StableId;
        public uint AllowedMaterialMask;
        public byte Displacement;
        public SurfaceDecorationShape DecorationShape;
        public byte DecorationDensity;
        public byte DecorationRadiusQ4;
        public byte DecorationHeightQ4;
        public byte DecorationDropQ4;
        public byte DecorationSeparation;
        public byte DecorationFaceMask;

        public bool Allows(byte materialId) =>
            materialId < 32 && (AllowedMaterialMask & (1u << materialId)) != 0;
    }

    public struct SurfaceJoinReadRule
    {
        public SurfaceCompatibility Compatibility;
        public SurfaceContinuity Continuity;
        public byte BlendWidth;
        public byte DominantGroup;
        public ushort TransitionStyleId;
        public bool PreserveSharpFeature;

        public static SurfaceJoinReadRule SharpSeam => new SurfaceJoinReadRule
        {
            Compatibility = SurfaceCompatibility.Seam,
            Continuity = SurfaceContinuity.Discontinuous,
            PreserveSharpFeature = true,
        };
    }

    /// <summary>Read-only material properties needed by presentation extraction.</summary>
    public interface IMaterialPresentationCatalogue
    {
        uint Version { get; }
        ushort GetDefaultSurfaceStyle(byte materialId);
    }

    /// <summary>Read-only surface catalogue used to capture a Burst-safe presentation snapshot.</summary>
    public interface ISurfacePresentationCatalogue
    {
        uint Version { get; }
        ulong CatalogueHash { get; }
        ulong ComputeHash();
        SurfaceStyleReadDefinition GetPresentation(ushort styleId);
        SurfaceJoinReadRule GetPresentationJoin(byte groupA, byte groupB);
    }

    /// <summary>Read-only coating catalogue used to capture a Burst-safe presentation snapshot.</summary>
    public interface ICoatingPresentationCatalogue
    {
        uint Version { get; }
        ulong CatalogueHash { get; }
        ulong ComputeHash();
        CoatingReadDefinition GetPresentation(byte coatingId);
    }

    public struct MaterialPaletteView
    {
        private struct Entry
        {
            public ushort DefaultSurfaceStyle;
        }

        private FixedList512Bytes<Entry> _entries;
        public uint Version { get; private set; }

        public static MaterialPaletteView Capture<T>(in T source)
            where T : struct, IMaterialPresentationCatalogue
        {
            MaterialPaletteView view = default;
            view.Version = source.Version;
            for (int i = 0; i < 32; i++)
                view._entries.Add(new Entry { DefaultSurfaceStyle = source.GetDefaultSurfaceStyle((byte)i) });
            return view;
        }

        public ushort GetDefaultSurfaceStyle(byte materialId) =>
            materialId < _entries.Length ? _entries[materialId].DefaultSurfaceStyle : SurfaceStyles.Smooth;
    }

    public struct SurfaceCatalogueView
    {
        public const int MaxStyles = 32;
        public const int MaxJoinGroups = 16;

        private FixedList512Bytes<SurfaceStyleReadDefinition> _styles;
        private FixedList4096Bytes<SurfaceJoinReadRule> _joins;

        public uint Version { get; private set; }
        public ulong CatalogueHash { get; private set; }

        public static SurfaceCatalogueView Capture<T>(in T source)
            where T : struct, ISurfacePresentationCatalogue
        {
            SurfaceCatalogueView view = default;
            view.Version = source.Version;
            view.CatalogueHash = source.CatalogueHash != 0 ? source.CatalogueHash : source.ComputeHash();
            for (int i = 0; i < MaxStyles; i++)
                view._styles.Add(source.GetPresentation((ushort)i));
            for (int a = 0; a < MaxJoinGroups; a++)
            for (int b = 0; b < MaxJoinGroups; b++)
                view._joins.Add(source.GetPresentationJoin((byte)a, (byte)b));
            return view;
        }

        public SurfaceStyleReadDefinition Get(ushort styleId)
        {
            if (styleId < _styles.Length) return _styles[styleId];
            return FallbackStyle(styleId);
        }

        public SurfaceJoinReadRule GetJoin(byte groupA, byte groupB)
        {
            if (groupA >= MaxJoinGroups || groupB >= MaxJoinGroups)
                return SurfaceJoinReadRule.SharpSeam;
            return _joins[groupA * MaxJoinGroups + groupB];
        }

        public ulong ComputeHash() => CatalogueHash;

        public void Seal(uint version, ulong catalogueHash)
        {
            Version = version;
            CatalogueHash = catalogueHash;
        }

        public static SurfaceCatalogueView CreateBuiltIns()
        {
            SurfaceCatalogueView view = default;
            for (ushort i = 0; i < MaxStyles; i++) view._styles.Add(FallbackStyle(i));
            for (int i = 0; i < MaxJoinGroups * MaxJoinGroups; i++) view._joins.Add(SurfaceJoinReadRule.SharpSeam);

            view._styles[SurfaceStyles.Smooth] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.Smooth, Reconstruction = SurfaceReconstruction.Smooth,
                Curvature = 255, JoinGroup = 1,
            };
            view._styles[SurfaceStyles.Planar] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.Planar, Reconstruction = SurfaceReconstruction.Planar,
                JoinGroup = 2, PreserveSharpFeatures = true,
            };
            view._styles[SurfaceStyles.Rounded] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.Rounded, Reconstruction = SurfaceReconstruction.Rounded,
                Curvature = 192, JoinGroup = 1,
            };
            view._styles[SurfaceStyles.Sharp] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.Sharp, Reconstruction = SurfaceReconstruction.Sharp,
                JoinGroup = 2, PreserveSharpFeatures = true,
            };
            view._styles[SurfaceStyles.MasonryJoint] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.MasonryJoint, Reconstruction = SurfaceReconstruction.Planar,
                Curvature = 0, JoinGroup = 3, PreserveSharpFeatures = true,
            };
            view._styles[SurfaceStyles.Beveled] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.Beveled, Reconstruction = SurfaceReconstruction.Rounded,
                Curvature = 96, JoinGroup = 2, PreserveSharpFeatures = true,
            };
            view._styles[SurfaceStyles.Cubic] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.Cubic, Reconstruction = SurfaceReconstruction.Cubic,
                JoinGroup = 2, PreserveSharpFeatures = true,
            };
            view._styles[SurfaceStyles.ArchitecturalRounded] = new SurfaceStyleReadDefinition
            {
                StableId = SurfaceStyles.ArchitecturalRounded,
                Reconstruction = SurfaceReconstruction.Rounded,
                Curvature = 224,
                JoinGroup = 4,
            };

            view.SetJoin(1, 1, new SurfaceJoinReadRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Smooth,
                BlendWidth = 2,
            });
            view.SetJoin(2, 2, new SurfaceJoinReadRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Discontinuous,
                PreserveSharpFeature = true,
            });
            view.SetJoin(3, 3, new SurfaceJoinReadRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Discontinuous,
                PreserveSharpFeature = true,
            });
            view.SetJoin(4, 4, new SurfaceJoinReadRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Smooth,
                BlendWidth = 4,
            });
            view.Version = 2;
            view.CatalogueHash = view.ComputeSemanticHash();
            return view;
        }

        private void SetJoin(byte a, byte b, SurfaceJoinReadRule rule)
        {
            _joins[a * MaxJoinGroups + b] = rule;
            _joins[b * MaxJoinGroups + a] = rule;
        }

        private static SurfaceStyleReadDefinition FallbackStyle(ushort styleId) => new SurfaceStyleReadDefinition
        {
            StableId = styleId,
            Reconstruction = SurfaceReconstruction.Sharp,
            JoinGroup = MaxJoinGroups - 1,
            PreserveSharpFeatures = true,
        };

        private ulong ComputeSemanticHash()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < _styles.Length; i++)
            {
                SurfaceStyleReadDefinition d = _styles[i];
                hash = (hash ^ (byte)d.Reconstruction) * prime;
                hash = (hash ^ d.Curvature) * prime;
                hash = (hash ^ d.JoinGroup) * prime;
                hash = (hash ^ (d.PreserveSharpFeatures ? (byte)1 : (byte)0)) * prime;
            }
            return hash;
        }
    }

    public struct CoatingCatalogueView
    {
        public const int MaxCoatings = 16;
        private FixedList512Bytes<CoatingReadDefinition> _coatings;
        public uint Version { get; private set; }
        public ulong CatalogueHash { get; private set; }

        public static CoatingCatalogueView Capture<T>(in T source)
            where T : struct, ICoatingPresentationCatalogue
        {
            CoatingCatalogueView view = default;
            view.Version = source.Version;
            view.CatalogueHash = source.CatalogueHash != 0 ? source.CatalogueHash : source.ComputeHash();
            for (int i = 0; i < MaxCoatings; i++) view._coatings.Add(source.GetPresentation((byte)i));
            return view;
        }

        public CoatingReadDefinition Get(byte id) =>
            id < _coatings.Length ? _coatings[id] : default;

        public ulong ComputeHash() => CatalogueHash;

        public void Seal(uint version, ulong catalogueHash)
        {
            Version = version;
            CatalogueHash = catalogueHash;
        }

        public static CoatingCatalogueView CreateBuiltIns()
        {
            CoatingCatalogueView view = default;
            for (int i = 0; i < MaxCoatings; i++) view._coatings.Add(default);
            view._coatings[Coatings.Moss] = new CoatingReadDefinition
            {
                StableId = Coatings.Moss, AllowedMaterialMask = uint.MaxValue,
                DecorationShape = SurfaceDecorationShape.Clump, DecorationDensity = 210,
                DecorationRadiusQ4 = 18, DecorationHeightQ4 = 2, DecorationDropQ4 = 18,
                DecorationFaceMask = 1 << 3,
            };
            view._coatings[Coatings.Snow] = new CoatingReadDefinition
            {
                StableId = Coatings.Snow, AllowedMaterialMask = uint.MaxValue, Displacement = 40,
            };
            view._coatings[Coatings.Soot] = new CoatingReadDefinition
            {
                StableId = Coatings.Soot, AllowedMaterialMask = uint.MaxValue, Displacement = 4,
            };
            view._coatings[Coatings.Wet] = new CoatingReadDefinition
            {
                StableId = Coatings.Wet, AllowedMaterialMask = uint.MaxValue,
            };
            view.Version = 4;
            view.CatalogueHash = view.ComputeSemanticHash();
            return view;
        }

        private ulong ComputeSemanticHash()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < _coatings.Length; i++)
            {
                CoatingReadDefinition d = _coatings[i];
                hash = (hash ^ d.StableId) * prime;
                hash = (hash ^ d.Displacement) * prime;
                hash = (hash ^ (byte)d.DecorationShape) * prime;
                hash = (hash ^ d.DecorationDensity) * prime;
            }
            return hash;
        }
    }
}
