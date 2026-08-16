using VoxelEngine.Storage.Api;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Compiles the game-owned material catalogue into Storage's semantic-free physical view.
    /// </summary>
    public static class GameMaterialSimulationDefinitions
    {
        public const int Count = GameMaterialRuntimeCatalogue.SimulationCount;

        public static MaterialDefinition[] Create()
        {
            var result = new MaterialDefinition[Count];
            int destination = 0;
            for (byte materialId = 0; materialId < GameMaterialRuntimeCatalogue.Count; materialId++)
            {
                ref readonly GameMaterialRuntimeDefinition row =
                    ref GameMaterialRuntimeCatalogue.Get(materialId);
                if (!row.HasSimulation) continue;
                result[destination++] = row.Simulation;
            }

            if (destination != result.Length)
                throw new System.InvalidOperationException(
                    "Game material simulation projection count does not match the authored catalogue.");
            return result;
        }
    }
}
