using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    /// <summary>
    /// Game-owned castle composition expressed through the reusable structure-authoring contracts.
    /// Castle semantics stay in the game layer while walls, towers, openings, battlements, footprint,
    /// and semantic materials remain shared configuration understood by other archetypes.
    /// </summary>
    public struct CastleComponentConfig
    {
        public StructureFootprintConfig BaileyFootprint;
        public StructureWallRunConfig CurtainWallX;
        public StructureWallRunConfig CurtainWallZ;
        public TowerConfig CornerTowers;
        public OpeningConfig MainGate;
        public BattlementConfig CurtainBattlements;
        public StructureMaterialPalette Palette;

        public bool IsWellFormed =>
            BaileyFootprint.IsWellFormed &&
            CurtainWallX.IsWellFormed &&
            CurtainWallZ.IsWellFormed &&
            CornerTowers.IsWellFormed &&
            MainGate.IsWellFormed &&
            CurtainBattlements.IsWellFormed;
    }
}
