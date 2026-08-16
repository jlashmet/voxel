using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// Semantic postern wrapper over the reusable arched wall-door realizer. Production spatial
    /// callers supply a frozen door recipe; compatibility callers receive the historical recipe.
    /// </summary>
    public static class CastlePosternRealizer
    {
        public static void CarveOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern)
        {
            CastleWallDoorPlan door = CastleWallDoorRecipe.PosternHistorical();
            CarveOpening(ref brush, in plan, in postern, in door);
        }

        public static void CarveOpening(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern,
            in CastleWallDoorPlan door) =>
            CastleWallDoorRealizer.CarveArchedOpening(
                ref brush,
                in plan,
                in postern,
                in door);

        public static void BuildDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern)
        {
            CastleWallDoorPlan door = CastleWallDoorRecipe.PosternHistorical();
            BuildDoor(ref brush, in plan, in postern, in door);
        }

        public static void BuildDoor(
            ref VoxelBrush brush,
            in CastlePlan plan,
            in CastleGatePlacementSpec postern,
            in CastleWallDoorPlan door) =>
            CastleWallDoorRealizer.BuildArchedDoor(
                ref brush,
                in plan,
                in postern,
                in door);
    }
}
