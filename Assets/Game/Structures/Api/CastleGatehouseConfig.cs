using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Castle-specific gatehouse composition over the shared structure component contracts.
    /// The gatehouse owns castle semantics (paired defensive towers, gate leaf/portcullis clearance,
    /// defended approach, and road attachment) while tower/opening/battlement behavior remains shared.
    /// </summary>
    public struct CastleGatehouseConfig
    {
        /// <summary>Overall defended gatehouse span along the curtain wall.</summary>
        public int Width;

        /// <summary>Overall gatehouse block depth through the curtain wall.</summary>
        public int Depth;

        /// <summary>Gatehouse block height before battlements.</summary>
        public int Height;

        /// <summary>Absolute X offset from gate centre to each flanking tower centre.</summary>
        public int TowerCentreOffset;

        /// <summary>Depth of the authored gate leaf inside the cleared opening.</summary>
        public int GateLeafDepth;

        public TowerConfig FlankingTowers;
        public OpeningConfig GateOpening;

        /// <summary>
        /// Clearance reserved for a future portcullis implementation. The compatibility authorer
        /// consumes this as the full-depth empty arch so adding a portcullis later does not require
        /// changing the immutable gatehouse envelope or inventing a second opening contract.
        /// </summary>
        public OpeningConfig PortcullisOpening;

        public BattlementConfig Battlements;
        public AttachmentAnchorConfig RoadAnchor;

        public bool IsWellFormed
        {
            get
            {
                if (Width <= 0 || Depth <= 0 || Height <= 0 ||
                    TowerCentreOffset <= 0 || GateLeafDepth <= 0 || GateLeafDepth > Depth)
                    return false;

                if (!FlankingTowers.IsWellFormed ||
                    FlankingTowers.Placement != StructureTowerPlacement.Explicit ||
                    FlankingTowers.Count != 2)
                    return false;

                if (!GateOpening.IsWellFormed || GateOpening.Kind != StructureOpeningKind.Arch ||
                    !PortcullisOpening.IsWellFormed ||
                    PortcullisOpening.Kind != StructureOpeningKind.Arch)
                    return false;

                if (GateOpening.Width >= Width || GateOpening.Height >= Height ||
                    PortcullisOpening.Width < GateOpening.Width ||
                    PortcullisOpening.Height < GateOpening.Height ||
                    PortcullisOpening.Width >= Width || PortcullisOpening.Height >= Height ||
                    PortcullisOpening.BottomOffset > GateOpening.BottomOffset)
                    return false;

                if (TowerCentreOffset * 2 > Width ||
                    TowerCentreOffset <= GateOpening.Width / 2)
                    return false;

                return Battlements.IsWellFormed &&
                       RoadAnchor.Kind == StructureAttachmentKind.Road &&
                       RoadAnchor.IsWellFormed;
            }
        }

        /// <summary>Resolves the configured castle-local road attachment into world coordinates.</summary>
        public int3 ResolveRoadAnchor(in CastlePlan plan) => plan.Centre + RoadAnchor.LocalPosition;
    }
}
