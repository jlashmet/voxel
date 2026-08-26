using System;

namespace MountingForce.WorldGen
{
    /// <summary>
    /// Topological entrance requested from architecture generation. Names describe physical behavior,
    /// not quest/story semantics; higher layers decide whether a realized hidden space is used as a secret.
    /// </summary>
    public enum HiddenSpaceEntranceKind : byte
    {
        BreakableMatchingWall = 0,
    }

    public enum HiddenSpaceVolumeKind : byte
    {
        SideCavity = 0,
    }

    /// <summary>
    /// Optional per-site request supplied after high-level site selection but before architecture/voxel
    /// realization. RequestId is an opaque stable correlation key owned by the caller.
    /// </summary>
    public sealed class SiteHiddenSpaceRequest
    {
        public string RequestId { get; }
        public int RoleId { get; }
        public int MinimumCount { get; }
        public int TargetCount { get; }
        public HiddenSpaceEntranceKind Entrance { get; }

        public SiteHiddenSpaceRequest(
            string requestId,
            int roleId,
            int minimumCount,
            int targetCount,
            HiddenSpaceEntranceKind entrance)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("Hidden-space request id is required.", nameof(requestId));
            if (roleId < 0)
                throw new ArgumentOutOfRangeException(nameof(roleId));
            if (minimumCount < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumCount));
            if (targetCount < minimumCount)
                throw new ArgumentOutOfRangeException(nameof(targetCount));

            RequestId = requestId;
            RoleId = roleId;
            MinimumCount = minimumCount;
            TargetCount = targetCount;
            Entrance = entrance;
        }
    }

    /// <summary>Axis-aligned local-space bounds in decimetres, relative to the owning site origin.</summary>
    public readonly struct HiddenSpaceBoundsDm
    {
        public int MinX { get; }
        public int MinY { get; }
        public int MinZ { get; }
        public int SizeX { get; }
        public int SizeY { get; }
        public int SizeZ { get; }

        public HiddenSpaceBoundsDm(
            int minX,
            int minY,
            int minZ,
            int sizeX,
            int sizeY,
            int sizeZ)
        {
            if (sizeX <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX));
            if (sizeY <= 0) throw new ArgumentOutOfRangeException(nameof(sizeY));
            if (sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeZ));

            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
        }
    }

    /// <summary>
    /// Physical entrance facts guaranteed by the generator. A higher layer may safely expose these as
    /// gameplay-secret semantics only when every required guarantee is true.
    /// </summary>
    public readonly struct HiddenSpaceEntranceRealization
    {
        public string Id { get; }
        public HiddenSpaceEntranceKind Kind { get; }
        public HiddenSpaceBoundsDm LocalBoundsDm { get; }
        public bool SeparatesHiddenSpaceBeforeOpen { get; }
        public bool GrantsNormalTraversalAfterOpen { get; }
        public bool IsStructurallyCritical { get; }
        public bool SupportsRemoval { get; }
        public bool MatchesHostSurface { get; }

        public HiddenSpaceEntranceRealization(
            string id,
            HiddenSpaceEntranceKind kind,
            HiddenSpaceBoundsDm localBoundsDm,
            bool separatesHiddenSpaceBeforeOpen,
            bool grantsNormalTraversalAfterOpen,
            bool isStructurallyCritical,
            bool supportsRemoval,
            bool matchesHostSurface)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Hidden-space entrance id is required.", nameof(id));

            Id = id;
            Kind = kind;
            LocalBoundsDm = localBoundsDm;
            SeparatesHiddenSpaceBeforeOpen = separatesHiddenSpaceBeforeOpen;
            GrantsNormalTraversalAfterOpen = grantsNormalTraversalAfterOpen;
            IsStructurallyCritical = isStructurallyCritical;
            SupportsRemoval = supportsRemoval;
            MatchesHostSurface = matchesHostSurface;
        }
    }

    /// <summary>
    /// Architecture-level realization of one physical hidden volume. Bounds are local to the site's
    /// authored orientation; voxel backends rotate/translate them with the same site placement as the host.
    /// </summary>
    public sealed class SiteHiddenSpaceRealization
    {
        public string RequestId { get; }
        public int RoleId { get; }
        public string CandidateId { get; }
        public HiddenSpaceVolumeKind Kind { get; }
        public HiddenSpaceBoundsDm LocalBoundsDm { get; }
        public bool HiddenFromNormalTraversal { get; }
        public int QualityBasisPoints { get; }
        public HiddenSpaceEntranceRealization Entrance { get; }

        public SiteHiddenSpaceRealization(
            string requestId,
            int roleId,
            string candidateId,
            HiddenSpaceVolumeKind kind,
            HiddenSpaceBoundsDm localBoundsDm,
            bool hiddenFromNormalTraversal,
            int qualityBasisPoints,
            HiddenSpaceEntranceRealization entrance)
        {
            if (string.IsNullOrWhiteSpace(requestId))
                throw new ArgumentException("Hidden-space request id is required.", nameof(requestId));
            if (roleId < 0)
                throw new ArgumentOutOfRangeException(nameof(roleId));
            if (string.IsNullOrWhiteSpace(candidateId))
                throw new ArgumentException("Hidden-space candidate id is required.", nameof(candidateId));
            if (qualityBasisPoints < 0 || qualityBasisPoints > 10000)
                throw new ArgumentOutOfRangeException(nameof(qualityBasisPoints));

            RequestId = requestId;
            RoleId = roleId;
            CandidateId = candidateId;
            Kind = kind;
            LocalBoundsDm = localBoundsDm;
            HiddenFromNormalTraversal = hiddenFromNormalTraversal;
            QualityBasisPoints = qualityBasisPoints;
            Entrance = entrance;
        }
    }
}
