using System;
using Unity.Mathematics;
using VoxelEngine.Vegetation.Api;

namespace MountingForce.CombatPrototype
{
    /// <summary>
    /// Narrow combat-facing environment contract. The deterministic board owns combat causality;
    /// implementations translate semantic combat events into production world-system calls.
    /// </summary>
    public interface IChainCombatEnvironmentBridge
    {
        void NotifyTreeImpact(GridPos treePosition, GridPos incomingDirection, int force);
        void NotifyTreeFelled(GridPos treePosition, GridPos fallDirection, int force);
    }

    /// <summary>
    /// Production vegetation adapter. Combat depends only on Vegetation.Api and metre-space values;
    /// Vegetation.Runtime remains responsible for tree lookup, caching and authoritative mutation.
    /// </summary>
    public sealed class ChainCombatVegetationBridge : IChainCombatEnvironmentBridge
    {
        private const float TreeSweepRadiusMetres = 0.65f;
        private const float FallBlastRadiusMetres = 2.25f;
        private readonly ITreeDamageService _treeDamage;

        public ChainCombatVegetationBridge(ITreeDamageService treeDamage)
        {
            _treeDamage = treeDamage ?? throw new ArgumentNullException(nameof(treeDamage));
        }

        public void NotifyTreeImpact(GridPos treePosition, GridPos incomingDirection, int force)
        {
            float3 tree = ToMetres(treePosition);
            float3 direction = Direction(incomingDirection);
            float distance = math.max(1f, force);
            float3 from = tree - direction * distance;
            _treeDamage.TrySweepImpact(from, tree, TreeSweepRadiusMetres, out _, out _);
        }

        public void NotifyTreeFelled(GridPos treePosition, GridPos fallDirection, int force)
        {
            float3 tree = ToMetres(treePosition);
            float3 impulse = Direction(fallDirection) * math.max(1f, force);
            _treeDamage.ApplyBlast(tree, FallBlastRadiusMetres, impulse);
        }

        private static float3 ToMetres(GridPos position) => new float3(position.X, 0f, position.Z);

        private static float3 Direction(GridPos direction)
        {
            float3 value = new float3(direction.X, 0f, direction.Z);
            return math.lengthsq(value) > 0f ? math.normalize(value) : new float3(0f, 0f, 1f);
        }
    }
}
