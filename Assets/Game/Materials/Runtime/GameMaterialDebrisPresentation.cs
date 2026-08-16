using Game.Materials.Api;
using Unity.Mathematics;

namespace Game.Materials.Runtime
{
    /// <summary>
    /// Game-owned material projection for detached-voxel debris. The showcase debris renderer
    /// consumes opaque material indices and asks the game for presentation/impulse policy.
    /// </summary>
    public static class GameMaterialDebrisPresentation
    {
        public static float ImpulseScale(byte materialId) =>
            materialId == GameMaterialIds.Wood ? 0.68f : 1.0f;

        public static float4 Colour(byte materialId)
        {
            switch (materialId)
            {
                case GameMaterialIds.Stone: return new float4(0.46f, 0.47f, 0.50f, 1f);
                case GameMaterialIds.Wood: return new float4(0.43f, 0.25f, 0.12f, 1f);
                case GameMaterialIds.Sand: return new float4(0.72f, 0.61f, 0.35f, 1f);
                case GameMaterialIds.Glass: return new float4(0.60f, 0.82f, 0.90f, 1f);
                case GameMaterialIds.Bedrock: return new float4(0.12f, 0.12f, 0.14f, 1f);
                default: return new float4(1f, 1f, 1f, 1f);
            }
        }
    }
}
