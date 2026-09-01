using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Complete cathedral entry point including shared exterior structural supports. Keeping this
    /// wrapper small makes the dependency explicit: CathedralAuthoring owns cathedral massing while
    /// the injected Structures API capability owns reusable buttress realization.
    /// </summary>
    public static class CathedralWorldbuildingAuthoring
    {
        public static void Author(
            IStructureComponentAuthoring components,
            IStructureAuthoringSession authoring,
            int3 origin,
            in CathedralWorldbuildingConfig config)
        {
            if (components == null) throw new System.ArgumentNullException(nameof(components));
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Cathedral worldbuilding configuration is invalid.", nameof(config));

            CathedralAuthoring.Author(components, authoring, origin, in config.Cathedral);
            if (!config.ButtressesEnabled) return;

            ChurchConfig church = config.Cathedral.Church;
            int frontZ = church.Footprint.Primary.Min.y;
            int assemblyWidth = config.Cathedral.NaveAssemblyWidth;
            var localAssembly = new StructureFootprintRect(
                new int2(-assemblyWidth / 2, frontZ),
                new int2(assemblyWidth, church.NaveLength));
            StructureFootprintRect world = StructureCardinalTransform.Rect(
                in localAssembly,
                church.EntryFacing);
            var min = new int3(
                origin.x + world.Min.x,
                origin.y,
                origin.z + world.Min.y);

            Facing west = StructureCardinalTransform.FacingDirection(Facing.West, church.EntryFacing);
            Facing east = StructureCardinalTransform.FacingDirection(Facing.East, church.EntryFacing);
            AuthorFacade(components, authoring, min, world.Size, west, church.NaveWalls.Height,
                in config.NaveButtresses, in church.Palette);
            AuthorFacade(components, authoring, min, world.Size, east, church.NaveWalls.Height,
                in config.NaveButtresses, in church.Palette);
        }

        private static void AuthorFacade(
            IStructureComponentAuthoring components,
            IStructureAuthoringSession authoring,
            int3 rectMin,
            int2 rectSize,
            Facing facade,
            int wallHeight,
            in ButtressConfig buttress,
            in StructureMaterialPalette palette)
        {
            int3 plane = rectMin;
            int span;
            if (facade == Facing.West)
                span = rectSize.y;
            else if (facade == Facing.East)
            {
                plane.x += rectSize.x;
                span = rectSize.y;
            }
            else if (facade == Facing.South)
                span = rectSize.x;
            else if (facade == Facing.North)
            {
                plane.z += rectSize.y;
                span = rectSize.x;
            }
            else
                throw new System.ArgumentOutOfRangeException(nameof(facade));

            var request = new StructureButtressAuthoringRequest
            {
                WallMin = plane,
                WallLength = span,
                WallHeight = wallHeight,
                Facade = facade,
                Buttress = buttress,
                Palette = palette,
            };
            components.AuthorButtresses(authoring, in request);
        }
    }
}
