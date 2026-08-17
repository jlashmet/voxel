using System;
using System.Runtime.CompilerServices;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Storage.Runtime
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

    public struct SurfaceStyleDefinition
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

    public struct CoatingDefinition
    {
        public byte StableId;
        public uint AllowedMaterialMask;
        public byte Displacement;
        public SurfaceDecorationShape DecorationShape;
        public byte DecorationDensity;
        public byte DecorationRadiusQ4;
        public byte DecorationHeightQ4;
        /// <summary>Maximum overhang below exposed ledges, in Q4 voxels.</summary>
        public byte DecorationDropQ4;
        public byte DecorationSeparation;
        /// <summary>Six face bits ordered -X,+X,-Y,+Y,-Z,+Z.</summary>
        public byte DecorationFaceMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Allows(byte materialId) =>
            materialId < 32 && (AllowedMaterialMask & (1u << materialId)) != 0;
    }

    public unsafe struct CoatingCatalogue : ICoatingAuthoringCatalogue, ICoatingPresentationCatalogue
    {
        public const uint BuiltInVersion = 4;
        public const int MaxCoatings = 16;
        private fixed uint _allowedMaterials[MaxCoatings];
        private fixed byte _displacement[MaxCoatings];
        private fixed byte _decorationShape[MaxCoatings];
        private fixed byte _decorationDensity[MaxCoatings];
        private fixed byte _decorationRadiusQ4[MaxCoatings];
        private fixed byte _decorationHeightQ4[MaxCoatings];
        private fixed byte _decorationDropQ4[MaxCoatings];
        private fixed byte _decorationSeparation[MaxCoatings];
        private fixed byte _decorationFaceMask[MaxCoatings];
        private fixed byte _registered[MaxCoatings];
        public uint Version { get; private set; }
        public ulong CatalogueHash { get; private set; }

        public void Register(in CoatingDefinition definition)
        {
            if (definition.StableId >= MaxCoatings) return;
            _allowedMaterials[definition.StableId] = definition.AllowedMaterialMask;
            _displacement[definition.StableId] = definition.Displacement;
            _decorationShape[definition.StableId] = (byte)definition.DecorationShape;
            _decorationDensity[definition.StableId] = definition.DecorationDensity;
            _decorationRadiusQ4[definition.StableId] = definition.DecorationRadiusQ4;
            _decorationHeightQ4[definition.StableId] = definition.DecorationHeightQ4;
            _decorationDropQ4[definition.StableId] = definition.DecorationDropQ4;
            _decorationSeparation[definition.StableId] = definition.DecorationSeparation;
            _decorationFaceMask[definition.StableId] = definition.DecorationFaceMask;
            _registered[definition.StableId] = 1;
            CatalogueHash = 0;
            Version++;
        }

        public CoatingDefinition Get(byte id)
        {
            if (id >= MaxCoatings || _registered[id] == 0) return default;
            return new CoatingDefinition
            {
                StableId = id,
                AllowedMaterialMask = _allowedMaterials[id],
                Displacement = _displacement[id],
                DecorationShape = (SurfaceDecorationShape)_decorationShape[id],
                DecorationDensity = _decorationDensity[id],
                DecorationRadiusQ4 = _decorationRadiusQ4[id],
                DecorationHeightQ4 = _decorationHeightQ4[id],
                DecorationDropQ4 = _decorationDropQ4[id],
                DecorationSeparation = _decorationSeparation[id],
                DecorationFaceMask = _decorationFaceMask[id],
            };
        }

        CoatingReadDefinition ICoatingPresentationCatalogue.GetPresentation(byte coatingId)
        {
            CoatingDefinition definition = Get(coatingId);
            return new CoatingReadDefinition
            {
                StableId = definition.StableId,
                AllowedMaterialMask = definition.AllowedMaterialMask,
                Displacement = definition.Displacement,
                DecorationShape = (VoxelEngine.Storage.Api.SurfaceDecorationShape)definition.DecorationShape,
                DecorationDensity = definition.DecorationDensity,
                DecorationRadiusQ4 = definition.DecorationRadiusQ4,
                DecorationHeightQ4 = definition.DecorationHeightQ4,
                DecorationDropQ4 = definition.DecorationDropQ4,
                DecorationSeparation = definition.DecorationSeparation,
                DecorationFaceMask = definition.DecorationFaceMask,
            };
        }

        public static implicit operator CoatingCatalogueView(CoatingCatalogue source) =>
            CoatingCatalogueView.Capture(in source);

        public bool Allows(byte coatingId, byte materialId) =>
            coatingId == Coatings.None || Get(coatingId).Allows(materialId);

        public bool IsRegistered(byte coatingId) =>
            coatingId == Coatings.None
            || coatingId < MaxCoatings && _registered[coatingId] != 0;

        public ulong ComputeHash()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < MaxCoatings; i++)
            {
                hash = (hash ^ _registered[i]) * prime;
                uint allowed = _allowedMaterials[i];
                hash = (hash ^ (byte)allowed) * prime;
                hash = (hash ^ (byte)(allowed >> 8)) * prime;
                hash = (hash ^ (byte)(allowed >> 16)) * prime;
                hash = (hash ^ (byte)(allowed >> 24)) * prime;
                hash = (hash ^ _displacement[i]) * prime;
                hash = (hash ^ _decorationShape[i]) * prime;
                hash = (hash ^ _decorationDensity[i]) * prime;
                hash = (hash ^ _decorationRadiusQ4[i]) * prime;
                hash = (hash ^ _decorationHeightQ4[i]) * prime;
                hash = (hash ^ _decorationDropQ4[i]) * prime;
                hash = (hash ^ _decorationSeparation[i]) * prime;
                hash = (hash ^ _decorationFaceMask[i]) * prime;
            }
            return hash;
        }

        public void Seal(uint version, ulong catalogueHash)
        {
            Version = version;
            CatalogueHash = catalogueHash;
        }

        public static CoatingCatalogue CreateBuiltIns()
        {
            CoatingCatalogue catalogue = default;
            catalogue.Register(new CoatingDefinition
            {
                StableId = Coatings.Moss, AllowedMaterialMask = uint.MaxValue, Displacement = 0,
                DecorationShape = SurfaceDecorationShape.Clump,
                DecorationDensity = 210, DecorationRadiusQ4 = 18,
                DecorationHeightQ4 = 2, DecorationDropQ4 = 18,
                DecorationSeparation = 0,
                DecorationFaceMask = 1 << 3,
            });
            catalogue.Register(new CoatingDefinition
            {
                StableId = Coatings.Snow, AllowedMaterialMask = uint.MaxValue, Displacement = 40
            });
            catalogue.Register(new CoatingDefinition
            {
                StableId = Coatings.Soot, AllowedMaterialMask = uint.MaxValue, Displacement = 4
            });
            catalogue.Register(new CoatingDefinition
            {
                StableId = Coatings.Wet, AllowedMaterialMask = uint.MaxValue, Displacement = 0
            });
            catalogue.Seal(BuiltInVersion, catalogue.ComputeHash());
            return catalogue;
        }
    }

    /// <summary>A symmetric, deterministic rule for a pair of surface join groups.</summary>
    public struct SurfaceJoinRule : IEquatable<SurfaceJoinRule>
    {
        public SurfaceCompatibility Compatibility;
        public SurfaceContinuity Continuity;
        public byte BlendWidth;
        public byte DominantGroup;
        public ushort TransitionStyleId;
        public bool PreserveSharpFeature;

        public static SurfaceJoinRule SharpSeam => new()
        {
            Compatibility = SurfaceCompatibility.Seam,
            Continuity = SurfaceContinuity.Discontinuous,
            PreserveSharpFeature = true
        };

        public bool Equals(SurfaceJoinRule other) =>
            Compatibility == other.Compatibility && Continuity == other.Continuity
            && BlendWidth == other.BlendWidth && DominantGroup == other.DominantGroup
            && TransitionStyleId == other.TransitionStyleId
            && PreserveSharpFeature == other.PreserveSharpFeature;
    }

    /// <summary>
    /// Fixed-capacity compiled surface catalogue. Its pair table canonicalizes group order on
    /// both reads and writes, making asymmetric curvature rules unrepresentable.
    /// </summary>
    public unsafe struct SurfaceCatalogue : ISurfaceStyleAuthoringCatalogue, ISurfacePresentationCatalogue
    {
        public const uint BuiltInVersion = 2;
        public const int MaxStyles = 32;
        public const int MaxJoinGroups = 16;
        private const int JoinRuleStride = 8;

        private fixed byte _reconstruction[MaxStyles];
        private fixed byte _curvature[MaxStyles];
        private fixed byte _joinGroup[MaxStyles];
        private fixed byte _preserveFeatures[MaxStyles];
        private fixed byte _registered[MaxStyles];
        private fixed byte _joinRules[MaxJoinGroups * MaxJoinGroups * JoinRuleStride];
        public uint Version { get; private set; }
        public ulong CatalogueHash { get; private set; }

        public void Register(in SurfaceStyleDefinition definition)
        {
            if (definition.StableId >= MaxStyles || definition.JoinGroup >= MaxJoinGroups) return;
            int i = definition.StableId;
            _reconstruction[i] = (byte)definition.Reconstruction;
            _curvature[i] = definition.Curvature;
            _joinGroup[i] = definition.JoinGroup;
            _preserveFeatures[i] = definition.PreserveSharpFeatures ? (byte)1 : (byte)0;
            _registered[i] = 1;
            CatalogueHash = 0;
            Version++;
        }

        public SurfaceStyleDefinition Get(ushort styleId)
        {
            if (styleId >= MaxStyles || styleId != SurfaceStyles.MaterialDefault
                && _registered[styleId] == 0)
            {
                return new SurfaceStyleDefinition
                {
                    StableId = styleId,
                    Reconstruction = SurfaceReconstruction.Sharp,
                    JoinGroup = MaxJoinGroups - 1,
                    PreserveSharpFeatures = true,
                };
            }
            return new SurfaceStyleDefinition
            {
                StableId = styleId,
                Reconstruction = (SurfaceReconstruction)_reconstruction[styleId],
                Curvature = _curvature[styleId],
                JoinGroup = _joinGroup[styleId],
                PreserveSharpFeatures = _preserveFeatures[styleId] != 0
            };
        }

        public bool IsRegistered(ushort styleId) =>
            styleId == SurfaceStyles.MaterialDefault
            || styleId < MaxStyles && _registered[styleId] != 0;

        public void SetJoin(byte groupA, byte groupB, in SurfaceJoinRule rule)
        {
            if (groupA >= MaxJoinGroups || groupB >= MaxJoinGroups) return;
            Canonicalize(ref groupA, ref groupB);
            int i = (groupA * MaxJoinGroups + groupB) * JoinRuleStride;
            _joinRules[i] = (byte)rule.Compatibility;
            _joinRules[i + 1] = (byte)rule.Continuity;
            _joinRules[i + 2] = rule.BlendWidth;
            _joinRules[i + 3] = rule.DominantGroup;
            _joinRules[i + 4] = (byte)rule.TransitionStyleId;
            _joinRules[i + 5] = (byte)(rule.TransitionStyleId >> 8);
            _joinRules[i + 6] = rule.PreserveSharpFeature ? (byte)1 : (byte)0;
            _joinRules[i + 7] = 1;
            CatalogueHash = 0;
            Version++;
        }

        public void Seal(uint version, ulong catalogueHash)
        {
            Version = version;
            CatalogueHash = catalogueHash;
        }

        public ulong ComputeHash()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < MaxStyles; i++)
            {
                hash = (hash ^ _registered[i]) * prime;
                hash = (hash ^ _reconstruction[i]) * prime;
                hash = (hash ^ _curvature[i]) * prime;
                hash = (hash ^ _joinGroup[i]) * prime;
                hash = (hash ^ _preserveFeatures[i]) * prime;
            }
            for (int i = 0; i < MaxJoinGroups * MaxJoinGroups * JoinRuleStride; i++)
                hash = (hash ^ _joinRules[i]) * prime;
            return hash;
        }

        public SurfaceJoinRule GetJoin(byte groupA, byte groupB)
        {
            if (groupA >= MaxJoinGroups || groupB >= MaxJoinGroups)
                return SurfaceJoinRule.SharpSeam;
            Canonicalize(ref groupA, ref groupB);
            int i = (groupA * MaxJoinGroups + groupB) * JoinRuleStride;
            if (_joinRules[i + 7] == 0) return SurfaceJoinRule.SharpSeam;
            return new SurfaceJoinRule
            {
                Compatibility = (SurfaceCompatibility)_joinRules[i],
                Continuity = (SurfaceContinuity)_joinRules[i + 1],
                BlendWidth = _joinRules[i + 2],
                DominantGroup = _joinRules[i + 3],
                TransitionStyleId = (ushort)(_joinRules[i + 4] | (_joinRules[i + 5] << 8)),
                PreserveSharpFeature = _joinRules[i + 6] != 0
            };
        }

        SurfaceStyleReadDefinition ISurfacePresentationCatalogue.GetPresentation(ushort styleId)
        {
            SurfaceStyleDefinition definition = Get(styleId);
            return new SurfaceStyleReadDefinition
            {
                StableId = definition.StableId,
                Reconstruction = (VoxelEngine.Storage.Api.SurfaceReconstruction)definition.Reconstruction,
                Curvature = definition.Curvature,
                JoinGroup = definition.JoinGroup,
                PreserveSharpFeatures = definition.PreserveSharpFeatures,
            };
        }

        SurfaceJoinReadRule ISurfacePresentationCatalogue.GetPresentationJoin(byte groupA, byte groupB)
        {
            SurfaceJoinRule rule = GetJoin(groupA, groupB);
            return new SurfaceJoinReadRule
            {
                Compatibility = (VoxelEngine.Storage.Api.SurfaceCompatibility)rule.Compatibility,
                Continuity = (VoxelEngine.Storage.Api.SurfaceContinuity)rule.Continuity,
                BlendWidth = rule.BlendWidth,
                DominantGroup = rule.DominantGroup,
                TransitionStyleId = rule.TransitionStyleId,
                PreserveSharpFeature = rule.PreserveSharpFeature,
            };
        }

        public static implicit operator SurfaceCatalogueView(SurfaceCatalogue source) =>
            SurfaceCatalogueView.Capture(in source);

        private static void Canonicalize(ref byte a, ref byte b)
        {
            if (a <= b) return;
            byte swap = a;
            a = b;
            b = swap;
        }

        public static SurfaceCatalogue CreateBuiltIns()
        {
            SurfaceCatalogue catalogue = default;
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.Smooth, Reconstruction = SurfaceReconstruction.Smooth,
                Curvature = 255, JoinGroup = 1
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.Planar, Reconstruction = SurfaceReconstruction.Planar,
                JoinGroup = 2, PreserveSharpFeatures = true
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.Rounded, Reconstruction = SurfaceReconstruction.Rounded,
                Curvature = 192, JoinGroup = 1
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.Sharp, Reconstruction = SurfaceReconstruction.Sharp,
                JoinGroup = 2, PreserveSharpFeatures = true
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.MasonryJoint,
                // Cut masonry is shaped by authored primitives. Blurring occupancy to recover
                // curvature melts voussoirs, arrises and openings; the extractor must preserve
                // those planes and let the annular primitive define the macro silhouette.
                Reconstruction = SurfaceReconstruction.Planar, Curvature = 0, JoinGroup = 3,
                PreserveSharpFeatures = true
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.Beveled,
                Reconstruction = SurfaceReconstruction.Rounded,
                Curvature = 96, JoinGroup = 2, PreserveSharpFeatures = true
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.Cubic,
                Reconstruction = SurfaceReconstruction.Cubic,
                JoinGroup = 2, PreserveSharpFeatures = true
            });
            catalogue.Register(new SurfaceStyleDefinition
            {
                StableId = SurfaceStyles.ArchitecturalRounded,
                Reconstruction = SurfaceReconstruction.Rounded,
                Curvature = 224,
                JoinGroup = 4
            });
            catalogue.SetJoin(1, 1, new SurfaceJoinRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Smooth,
                BlendWidth = 2
            });
            catalogue.SetJoin(2, 2, new SurfaceJoinRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Discontinuous,
                PreserveSharpFeature = true
            });
            catalogue.SetJoin(3, 3, new SurfaceJoinRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Discontinuous,
                PreserveSharpFeature = true
            });
            catalogue.SetJoin(4, 4, new SurfaceJoinRule
            {
                Compatibility = SurfaceCompatibility.Join,
                Continuity = SurfaceContinuity.Smooth,
                BlendWidth = 4
            });
            catalogue.Seal(BuiltInVersion, catalogue.ComputeHash());
            return catalogue;
        }
    }
}
