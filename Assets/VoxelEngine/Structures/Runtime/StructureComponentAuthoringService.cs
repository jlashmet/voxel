using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Runtime implementation of the public architectural-component authoring capability.</summary>
    public sealed class StructureComponentAuthoringService : IStructureComponentAuthoring
    {
        public void AuthorOpenings(
            IStructureAuthoringSession authoring,
            in StructureOpeningAuthoringRequest request)
        {
            StructureOpeningAuthoring.AuthorRepeated(
                authoring,
                request.ShellMin,
                request.Width,
                request.Height,
                request.Depth,
                request.WallThickness,
                in request.Opening,
                request.Count,
                request.Facade,
                request.GroupOffset,
                request.Spacing,
                in request.Palette);
        }

        public void AuthorRoof(
            IStructureAuthoringSession authoring,
            in StructureRoofAuthoringRequest request)
        {
            StructureRoofAuthoring.Author(
                authoring,
                request.FootprintMin,
                request.Width,
                request.Depth,
                request.BaseY,
                in request.Roof,
                request.Material);
        }

        public void AuthorStair(
            IStructureAuthoringSession authoring,
            in StructureStairAuthoringRequest request)
        {
            StructureStairAuthoring.Author(
                authoring,
                request.BottomCentre,
                request.AscentDirection,
                in request.Stair,
                in request.Palette);
        }

        public void AuthorColumn(
            IStructureAuthoringSession authoring,
            in StructureColumnAuthoringRequest request)
        {
            StructureColumnAuthoring.AuthorColumn(
                authoring,
                request.BaseCentre,
                in request.Column,
                in request.Palette);
        }

        public void AuthorButtresses(
            IStructureAuthoringSession authoring,
            in StructureButtressAuthoringRequest request)
        {
            StructureButtressAuthoring.AuthorRepeated(
                authoring,
                request.WallMin,
                request.WallLength,
                request.WallHeight,
                request.Facade,
                in request.Buttress,
                in request.Palette);
        }
    }
}
