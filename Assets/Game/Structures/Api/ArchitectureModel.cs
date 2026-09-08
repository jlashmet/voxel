using System;
using Unity.Mathematics;

namespace Game.Structures.Api
{
    public enum ArchitectureStructureClass : byte
    {
        Unknown = 0,
        Residence = 1,
        Shop = 2,
        Inn = 3,
        Church = 4,
        Warehouse = 5,
        Civic = 6,
        Castle = 7,
        Landmark = 8,
        GuildHall = 9,
    }

    public enum ArchitectureRoofForm : byte
    {
        ProviderDefault = 0,
        Flat = 1,
        Gable = 2,
        Hip = 3,
        Tower = 4,
        Open = 5,
    }

    public enum ArchitectureFoundationForm : byte
    {
        ProviderDefault = 0,
        Slab = 1,
        Raised = 2,
        Masonry = 3,
    }

    public enum ArchitectureOpeningPattern : byte
    {
        ProviderDefault = 0,
        Sparse = 1,
        Regular = 2,
        Generous = 3,
    }

    [Flags]
    public enum ArchitectureParameterSupport : ushort
    {
        None = 0,
        Footprint = 1 << 0,
        StoreyCount = 1 << 1,
        FloorHeight = 1 << 2,
        RoomCount = 1 << 3,
        RoofForm = 1 << 4,
        Foundation = 1 << 5,
        OpeningPattern = 1 << 6,
        Trim = 1 << 7,
        Detail = 1 << 8,
    }

    [Flags]
    public enum ArchitecturePresentationCapabilities : ushort
    {
        None = 0,
        ProductionVoxelOccupancy = 1 << 0,
        ProductionMaterials = 1 << 1,
        ProductionMaterialTextures = 1 << 2,
        SignedDistancePresentation = 1 << 3,
        CollisionFromOccupancy = 1 << 4,
        InteriorShell = 1 << 5,
        TraversableOpenings = 1 << 6,
        MultiStoreyCirculation = 1 << 7,
    }

    public readonly struct ArchitectureStyleProfileDescriptor
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly DecorationStyleFamily StyleFamily;

        public ArchitectureStyleProfileDescriptor(
            string key,
            string displayName,
            DecorationStyleFamily styleFamily)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            StyleFamily = styleFamily;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(Key) &&
            !string.IsNullOrWhiteSpace(DisplayName) &&
            StyleFamily != DecorationStyleFamily.Unknown;
    }

    public readonly struct ArchitectureProviderDescriptor
    {
        public readonly string Key;
        public readonly string DisplayName;
        public readonly ArchitectureStructureClass StructureClass;
        public readonly string[] ArchetypeKeys;
        public readonly ArchitectureParameterSupport SupportedParameters;
        public readonly ArchitecturePresentationCapabilities Capabilities;

        public ArchitectureProviderDescriptor(
            string key,
            string displayName,
            ArchitectureStructureClass structureClass,
            string[] archetypeKeys,
            ArchitectureParameterSupport supportedParameters,
            ArchitecturePresentationCapabilities capabilities)
        {
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            StructureClass = structureClass;
            ArchetypeKeys = archetypeKeys ?? Array.Empty<string>();
            SupportedParameters = supportedParameters;
            Capabilities = capabilities;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(Key) &&
            !string.IsNullOrWhiteSpace(DisplayName) &&
            StructureClass != ArchitectureStructureClass.Unknown &&
            ArchetypeKeys != null &&
            ArchetypeKeys.Length > 0 &&
            SupportedParameters != ArchitectureParameterSupport.None &&
            (Capabilities & ArchitecturePresentationCapabilities.ProductionVoxelOccupancy) != 0;
    }

    /// <summary>
    /// Reusable architecture parameters expressed in authoritative voxel units. A zero numeric value
    /// requests the provider default. Providers must reject unsupported non-default fields explicitly.
    /// </summary>
    public readonly struct ArchitectureGenerationParameters
    {
        public readonly int Width;
        public readonly int Depth;
        public readonly int StoreyCount;
        public readonly int FloorHeight;
        public readonly int RoomCount;
        public readonly ArchitectureRoofForm RoofForm;
        public readonly ArchitectureFoundationForm Foundation;
        public readonly ArchitectureOpeningPattern Openings;
        public readonly byte TrimLevel;
        public readonly byte DetailLevel;

        public ArchitectureGenerationParameters(
            int width = 0,
            int depth = 0,
            int storeyCount = 0,
            int floorHeight = 0,
            int roomCount = 0,
            ArchitectureRoofForm roofForm = ArchitectureRoofForm.ProviderDefault,
            ArchitectureFoundationForm foundation = ArchitectureFoundationForm.ProviderDefault,
            ArchitectureOpeningPattern openings = ArchitectureOpeningPattern.ProviderDefault,
            byte trimLevel = 0,
            byte detailLevel = 0)
        {
            Width = width;
            Depth = depth;
            StoreyCount = storeyCount;
            FloorHeight = floorHeight;
            RoomCount = roomCount;
            RoofForm = roofForm;
            Foundation = foundation;
            Openings = openings;
            TrimLevel = trimLevel;
            DetailLevel = detailLevel;
        }

        public bool IsWellFormed =>
            Width >= 0 && Depth >= 0 && StoreyCount >= 0 && FloorHeight >= 0 && RoomCount >= 0 &&
            (byte)RoofForm <= (byte)ArchitectureRoofForm.Open &&
            (byte)Foundation <= (byte)ArchitectureFoundationForm.Masonry &&
            (byte)Openings <= (byte)ArchitectureOpeningPattern.Generous &&
            TrimLevel <= 4 && DetailLevel <= 4;
    }

    public readonly struct ArchitectureGenerationRequest
    {
        public readonly string StyleProfileKey;
        public readonly string ProviderKey;
        public readonly string ArchetypeKey;
        public readonly uint Seed;
        public readonly uint StructureId;
        public readonly int3 Origin;
        public readonly ArchitectureGenerationParameters Parameters;

        public ArchitectureGenerationRequest(
            string styleProfileKey,
            string providerKey,
            string archetypeKey,
            uint seed,
            uint structureId,
            int3 origin,
            ArchitectureGenerationParameters parameters)
        {
            StyleProfileKey = styleProfileKey ?? string.Empty;
            ProviderKey = providerKey ?? string.Empty;
            ArchetypeKey = archetypeKey ?? string.Empty;
            Seed = seed;
            StructureId = structureId;
            Origin = origin;
            Parameters = parameters;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(StyleProfileKey) &&
            !string.IsNullOrWhiteSpace(ProviderKey) &&
            !string.IsNullOrWhiteSpace(ArchetypeKey) &&
            StructureId != 0 &&
            Parameters.IsWellFormed;
    }

    public readonly struct ArchitectureRealizationSummary
    {
        public readonly int3 Min;
        public readonly int3 MaxExclusive;
        public readonly int StoreyCount;
        public readonly int FloorHeight;
        public readonly int DoorCount;
        public readonly int WindowCount;
        public readonly int StairCount;
        public readonly bool HasInteriorShell;
        public readonly ArchitecturePresentationCapabilities Capabilities;

        public ArchitectureRealizationSummary(
            int3 min,
            int3 maxExclusive,
            int storeyCount,
            int floorHeight,
            int doorCount,
            int windowCount,
            int stairCount,
            bool hasInteriorShell,
            ArchitecturePresentationCapabilities capabilities)
        {
            Min = min;
            MaxExclusive = maxExclusive;
            StoreyCount = storeyCount;
            FloorHeight = floorHeight;
            DoorCount = doorCount;
            WindowCount = windowCount;
            StairCount = stairCount;
            HasInteriorShell = hasInteriorShell;
            Capabilities = capabilities;
        }

        public bool IsWellFormed =>
            math.all(MaxExclusive > Min) &&
            StoreyCount > 0 && FloorHeight > 0 &&
            DoorCount > 0 && WindowCount >= 0 && StairCount >= 0 &&
            (Capabilities & ArchitecturePresentationCapabilities.ProductionVoxelOccupancy) != 0;
    }

    public readonly struct ArchitectureGenerationIdentity
    {
        public readonly ulong RequestHash;
        public readonly ulong RealizationHash;

        public ArchitectureGenerationIdentity(ulong requestHash, ulong realizationHash)
        {
            RequestHash = requestHash;
            RealizationHash = realizationHash;
        }

        public bool IsWellFormed => RequestHash != 0 && RealizationHash != 0;
    }

    public readonly struct ArchitectureGenerationResult
    {
        public readonly ArchitectureGenerationIdentity Identity;
        public readonly ArchitectureRealizationSummary Summary;

        public ArchitectureGenerationResult(
            ArchitectureGenerationIdentity identity,
            ArchitectureRealizationSummary summary)
        {
            Identity = identity;
            Summary = summary;
        }

        public bool IsWellFormed => Identity.IsWellFormed && Summary.IsWellFormed;
    }

    public enum ArchitectureReviewPoseKind : byte
    {
        Exterior = 1,
        Interior = 2,
    }

    public readonly struct ArchitectureReferenceImageDescriptor
    {
        public readonly string Key;
        public readonly string AssetPath;
        public readonly string Caption;

        public ArchitectureReferenceImageDescriptor(string key, string assetPath, string caption)
        {
            Key = key ?? string.Empty;
            AssetPath = assetPath ?? string.Empty;
            Caption = caption ?? string.Empty;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(Key) && !string.IsNullOrWhiteSpace(AssetPath);
    }

    /// <summary>Presentation-only deterministic camera pose relative to the generated structure origin.</summary>
    public readonly struct ArchitectureReviewPose
    {
        public readonly string Key;
        public readonly ArchitectureReviewPoseKind Kind;
        public readonly float3 EyeOffsetMeters;
        public readonly float3 LookAtOffsetMeters;
        public readonly float FieldOfView;

        public ArchitectureReviewPose(
            string key,
            ArchitectureReviewPoseKind kind,
            float3 eyeOffsetMeters,
            float3 lookAtOffsetMeters,
            float fieldOfView)
        {
            Key = key ?? string.Empty;
            Kind = kind;
            EyeOffsetMeters = eyeOffsetMeters;
            LookAtOffsetMeters = lookAtOffsetMeters;
            FieldOfView = fieldOfView;
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(Key) &&
            (Kind == ArchitectureReviewPoseKind.Exterior || Kind == ArchitectureReviewPoseKind.Interior) &&
            FieldOfView >= 20f && FieldOfView <= 100f;
    }

    public readonly struct ArchitectureCanonicalReviewDescriptor
    {
        public readonly string Key;
        public readonly ArchitectureGenerationRequest Request;
        public readonly ArchitectureReferenceImageDescriptor[] References;
        public readonly ArchitectureReviewPose[] Poses;

        public ArchitectureCanonicalReviewDescriptor(
            string key,
            ArchitectureGenerationRequest request,
            ArchitectureReferenceImageDescriptor[] references,
            ArchitectureReviewPose[] poses)
        {
            Key = key ?? string.Empty;
            Request = request;
            References = references ?? Array.Empty<ArchitectureReferenceImageDescriptor>();
            Poses = poses ?? Array.Empty<ArchitectureReviewPose>();
        }

        public bool IsWellFormed =>
            !string.IsNullOrWhiteSpace(Key) &&
            Request.IsWellFormed &&
            Poses != null && Poses.Length > 0;
    }
}
