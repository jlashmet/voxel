using Game.Materials.Api;
using Unity.Mathematics;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned material projection for detached-voxel debris. The showcase debris renderer
    /// consumes opaque material indices and asks the game for presentation/impulse policy.
    /// Values intentionally preserve the existing showcase behavior during ownership migration.
    /// </summary>
    public static class GameMaterialDebrisPresentation
    {
        public static float ImpulseScale(byte materialId)
        {
            switch (materialId)
            {
                case GameMaterialIds.Wood: return 0.58f;
                case GameMaterialIds.Cloth: return 0.50f;
                case GameMaterialIds.Grass: return 0.38f;
                case GameMaterialIds.Moss: return 0.45f;
                default: return 1.0f;
            }
        }

        public static float4 Colour(byte materialId, float alpha)
        {
            switch (materialId)
            {
                case GameMaterialIds.Wood: return new float4(0.43f, 0.25f, 0.12f, alpha);
                case GameMaterialIds.Sand: return new float4(0.72f, 0.64f, 0.42f, alpha);
                case GameMaterialIds.Glass: return new float4(0.52f, 0.78f, 0.88f, alpha);
                case GameMaterialIds.Slate: return new float4(0.20f, 0.24f, 0.30f, alpha);
                case GameMaterialIds.Tile: return new float4(0.42f, 0.18f, 0.12f, alpha);
                case GameMaterialIds.Grass: return new float4(0.25f, 0.46f, 0.15f, alpha);
                case GameMaterialIds.Dirt: return new float4(0.32f, 0.22f, 0.13f, alpha);
                case GameMaterialIds.Moss: return new float4(0.22f, 0.38f, 0.18f, alpha);
                case GameMaterialIds.LitWindow: return new float4(0.18f, 0.20f, 0.19f, alpha);
                case GameMaterialIds.Cascade: return new float4(0.22f, 0.62f, 0.78f, alpha);
                case GameMaterialIds.Crystal: return new float4(0.08f, 0.56f, 0.82f, alpha);
                case GameMaterialIds.MasonrySmall: return new float4(0.65f, 0.56f, 0.41f, alpha);
                case GameMaterialIds.MasonryMedium: return new float4(0.68f, 0.58f, 0.42f, alpha);
                case GameMaterialIds.MasonryLarge: return new float4(0.63f, 0.54f, 0.40f, alpha);
                default: return new float4(0.48f, 0.50f, 0.54f, alpha);
            }
        }
    }
}
