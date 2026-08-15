using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Where a definition may appear.
    ///
    /// Every field is an integer, including the probability, which is expressed out of 65536
    /// rather than as a fraction. A float here would be a float in placement, and placement is
    /// cross-client agreement: two clients that disagree about whether a village exists have
    /// diverged in the way Constitution I exists to prevent, and neither can detect it alone.
    /// </summary>
    public struct PlacementRule
    {
        public int DefinitionId;

        /// <summary>
        /// Placement lattice cell edge in voxels.
        ///
        /// Candidates are drawn per cell from a hash of the cell coordinate, which is what makes
        /// placement computable by any region without consulting a neighbour.
        /// </summary>
        public int CellEdge;

        /// <summary>Candidates drawn per cell before filtering.</summary>
        public int AttemptsPerCell;

        /// <summary>Acceptance probability out of 65536.</summary>
        public int AcceptProbability;

        /// <summary>Inclusive altitude band, in voxels.</summary>
        public int MinAltitude;
        public int MaxAltitude;

        /// <summary>Steepest ground the rule allows, combined with the definition's own limit.</summary>
        public int MaxSlope;

        /// <summary>
        /// Minimum spacing between instances of this definition, in voxels.
        ///
        /// Must not exceed <see cref="CellEdge"/>: spacing is enforced against candidates in the
        /// neighbouring cells a region already scans, and a spacing larger than that neighbourhood
        /// could only be enforced with knowledge the region does not have.
        /// </summary>
        public int MinSpacing;

        /// <summary>Instances per accepted cluster.</summary>
        public int ClusterMin;
        public int ClusterMax;

        /// <summary>Bit per exclusion class this rule respects — cave mouths, protected zones.</summary>
        public int ExclusionMask;

        /// <summary>Range into the catalogue's explicit placement pool.</summary>
        public int ExplicitOffset, ExplicitCount;

        /// <summary>Spacing is only enforceable within the scanned neighbourhood.</summary>
        public bool SpacingEnforceable => MinSpacing <= CellEdge;
    }

    /// <summary>
    /// A feature placed at an authored coordinate rather than by rule — the landmark that has to
    /// be exactly there.
    ///
    /// Explicit placements bypass acceptance probability and clustering but not validation: an
    /// explicitly placed structure still adapts to terrain and still loses contested space to
    /// higher precedence.
    /// </summary>
    public struct ExplicitPlacement
    {
        public int3 Position;

        /// <summary>One of four cardinal rotations. Integer, so placement needs no matrix.</summary>
        public byte Orientation;

        /// <summary>
        /// Range into the catalogue's parameter-override pool. A value of -1 in that pool means
        /// "draw this one", so an author can pin the parameters that matter and leave the rest to
        /// vary.
        /// </summary>
        public int OverrideOffset, OverrideCount;
    }
}
