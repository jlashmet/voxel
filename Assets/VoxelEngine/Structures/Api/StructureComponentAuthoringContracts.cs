using Unity.Mathematics;

namespace VoxelEngine.Structures.Api
{
    public struct StructureOpeningAuthoringRequest
    {
        public int3 ShellMin;
        public int Width;
        public int Height;
        public int Depth;
        public int WallThickness;
        public OpeningConfig Opening;
        public int Count;
        public Facing Facade;
        public int GroupOffset;
        public int Spacing;
        public StructureMaterialPalette Palette;
    }

    public struct StructureRoofAuthoringRequest
    {
        public int3 FootprintMin;
        public int Width;
        public int Depth;
        public int BaseY;
        public RoofConfig Roof;
        public byte Material;
    }

    public struct StructureStairAuthoringRequest
    {
        public int3 BottomCentre;
        public Facing AscentDirection;
        public StairConfig Stair;
        public StructureMaterialPalette Palette;
    }

    public struct StructureColumnAuthoringRequest
    {
        public int3 BaseCentre;
        public ColumnConfig Column;
        public StructureMaterialPalette Palette;
    }

    public struct StructureButtressAuthoringRequest
    {
        public int3 WallMin;
        public int WallLength;
        public int WallHeight;
        public Facing Facade;
        public ButtressConfig Buttress;
        public StructureMaterialPalette Palette;
    }

    /// <summary>
    /// Narrow execution boundary for reusable architectural components already consumed by game
    /// structure composition. Game code selects semantic configs and materials; Runtime owns voxel
    /// emission and validation.
    /// </summary>
    public interface IStructureComponentAuthoring
    {
        void AuthorOpenings(IStructureAuthoringSession authoring, in StructureOpeningAuthoringRequest request);
        void AuthorRoof(IStructureAuthoringSession authoring, in StructureRoofAuthoringRequest request);
        void AuthorStair(IStructureAuthoringSession authoring, in StructureStairAuthoringRequest request);
        void AuthorColumn(IStructureAuthoringSession authoring, in StructureColumnAuthoringRequest request);
        void AuthorButtresses(IStructureAuthoringSession authoring, in StructureButtressAuthoringRequest request);
    }
}
