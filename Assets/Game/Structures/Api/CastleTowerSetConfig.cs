using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Castle composition for one deterministic tower group. Shape, dimensions, count/placement,
    /// top style, roof, and openings remain in the shared TowerConfig; castle-only taper,
    /// crenellation policy, and optional explicit perimeter positions live here.
    /// </summary>
    public struct CastleTowerGroupConfig
    {
        public TowerConfig Towers;
        public int Taper;
        public BattlementConfig Crenellations;
        public FixedList512Bytes<int2> ExplicitPositions;

        public bool IsWellFormed
        {
            get
            {
                if (!Towers.IsWellFormed || Taper < 0 || !Crenellations.IsWellFormed)
                    return false;

                if (Towers.Shape == StructureTowerShape.Round && Taper >= Towers.Radius)
                    return false;
                if (Towers.Shape == StructureTowerShape.Square &&
                    (Taper * 2 >= Towers.Width || Taper * 2 >= Towers.Depth))
                    return false;

                if (Towers.Placement == StructureTowerPlacement.Explicit)
                    return ExplicitPositions.Length == Towers.Count;

                return ExplicitPositions.Length == 0;
            }
        }
    }

    /// <summary>
    /// Corner towers plus an optional intermediate perimeter group. Gatehouse flanking towers stay
    /// under gatehouse configuration because they have different placement/entrance semantics.
    /// </summary>
    public struct CastleTowerSetConfig
    {
        public CastleTowerGroupConfig Corners;
        public bool IntermediateEnabled;
        public CastleTowerGroupConfig Intermediate;

        public bool IsWellFormed =>
            Corners.IsWellFormed && (!IntermediateEnabled || Intermediate.IsWellFormed);
    }

    public static class CastleTowerSetPresets
    {
        /// <summary>Preserves the current four-corner round-tower policy; intermediates start off.</summary>
        public static CastleTowerSetConfig Compatibility(in CastleComponentConfig components)
        {
            TowerConfig corners = components.CornerTowers;
            corners.Placement = StructureTowerPlacement.Corners;
            corners.Count = 4;

            return new CastleTowerSetConfig
            {
                Corners = new CastleTowerGroupConfig
                {
                    Towers = corners,
                    Taper = 0,
                    Crenellations = components.CurtainBattlements,
                },
                IntermediateEnabled = false,
            };
        }
    }
}
