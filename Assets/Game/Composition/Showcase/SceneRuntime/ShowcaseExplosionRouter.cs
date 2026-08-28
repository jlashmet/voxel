using System;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Selects the one legal mutation owner for a showcase destruction impact.
    /// Connected clients request server authority; offline showcases retain the local deterministic
    /// edit path. A connected request that is temporarily not ready must never fall back to a local
    /// mutation, because that would create the divergence this boundary exists to prevent.
    /// </summary>
    public static class ShowcaseExplosionRouter
    {
        public static ShowcaseExplosionRouteResult Apply(
            IShowcaseExplosionWorld world,
            IShowcaseExplosionNetwork network,
            int3 originVoxel,
            int radiusVoxels,
            float3 impulseDirection)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            bool networked = network != null && network.IsActive;
            if (networked)
            {
                bool sent = network.TryRequestExplosion(originVoxel, radiusVoxels);
                return new ShowcaseExplosionRouteResult(true, sent, 0);
            }

            int changed = world.Explode(
                originVoxel,
                (ushort)math.clamp(radiusVoxels, 1, ushort.MaxValue),
                impulseDirection);
            return new ShowcaseExplosionRouteResult(false, false, changed);
        }
    }

    public readonly struct ShowcaseExplosionRouteResult
    {
        public ShowcaseExplosionRouteResult(bool networked, bool requestSent, int changedVoxels)
        {
            Networked = networked;
            RequestSent = requestSent;
            ChangedVoxels = changedVoxels;
        }

        public bool Networked { get; }
        public bool RequestSent { get; }
        public int ChangedVoxels { get; }
    }

    public interface IShowcaseExplosionWorld
    {
        int Explode(int3 originVoxel, ushort radiusVoxels, float3 impulseDirection);
    }

    public interface IShowcaseExplosionNetwork
    {
        bool IsActive { get; }
        bool TryRequestExplosion(int3 originVoxel, int radiusVoxels);
    }

    internal sealed class ShowcaseWorldExplosionAdapter : IShowcaseExplosionWorld
    {
        private readonly ShowcaseWorld _world;

        public ShowcaseWorldExplosionAdapter(ShowcaseWorld world) =>
            _world = world ?? throw new ArgumentNullException(nameof(world));

        public int Explode(int3 originVoxel, ushort radiusVoxels, float3 impulseDirection) =>
            _world.Explode(originVoxel, radiusVoxels, impulseDirection);
    }

    internal sealed class ShowcaseSessionExplosionAdapter : IShowcaseExplosionNetwork
    {
        private readonly ShowcaseMultiplayerSession _session;

        public ShowcaseSessionExplosionAdapter(ShowcaseMultiplayerSession session) =>
            _session = session ?? throw new ArgumentNullException(nameof(session));

        public bool IsActive => _session.IsActive;

        public bool TryRequestExplosion(int3 originVoxel, int radiusVoxels) =>
            _session.TryRequestExplosion(originVoxel, radiusVoxels);
    }
}
