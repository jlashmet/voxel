using System;
using Unity.Mathematics;

namespace VoxelEngine.Rendering.Api
{
    /// <summary>
    /// Presentation-only tree representation selected from existing semantic tree truth.
    /// This does not own tree placement, damage, skeletons, or persistence.
    /// </summary>
    public enum TreePresentationTier : byte
    {
        Culled = 0,
        Full = 1,
        Simplified = 2,
        CanopyMember = 3,
        Landmark = 4,
    }

    public readonly struct TreePresentationInput
    {
        public TreePresentationInput(
            ulong stableId,
            float3 positionMetres,
            float scale,
            float health01,
            bool severed,
            bool landmark)
        {
            StableId = stableId;
            PositionMetres = positionMetres;
            Scale = math.max(0.01f, scale);
            Health01 = math.clamp(health01, 0f, 1f);
            Severed = severed;
            Landmark = landmark;
        }

        public ulong StableId { get; }
        public float3 PositionMetres { get; }
        public float Scale { get; }
        public float Health01 { get; }
        public bool Severed { get; }
        public bool Landmark { get; }
    }

    /// <summary>
    /// Composition-owned thresholds for choosing tree representation. The renderer consumes the
    /// selected tier; it does not decide semantic importance or persistence.
    /// </summary>
    public readonly struct TreeVisibilityTierPolicy
    {
        public TreeVisibilityTierPolicy(
            float fullExitMetres,
            float simplifiedExitMetres,
            float canopyExitMetres,
            float landmarkExitMetres,
            float hysteresisMetres = 12f)
        {
            if (!(fullExitMetres > 0f)) throw new ArgumentOutOfRangeException(nameof(fullExitMetres));
            if (simplifiedExitMetres < fullExitMetres) throw new ArgumentOutOfRangeException(nameof(simplifiedExitMetres));
            if (canopyExitMetres < simplifiedExitMetres) throw new ArgumentOutOfRangeException(nameof(canopyExitMetres));
            if (landmarkExitMetres < canopyExitMetres) throw new ArgumentOutOfRangeException(nameof(landmarkExitMetres));
            if (hysteresisMetres < 0f) throw new ArgumentOutOfRangeException(nameof(hysteresisMetres));

            FullExitMetres = fullExitMetres;
            SimplifiedExitMetres = simplifiedExitMetres;
            CanopyExitMetres = canopyExitMetres;
            LandmarkExitMetres = landmarkExitMetres;
            HysteresisMetres = hysteresisMetres;
        }

        public float FullExitMetres { get; }
        public float SimplifiedExitMetres { get; }
        public float CanopyExitMetres { get; }
        public float LandmarkExitMetres { get; }
        public float HysteresisMetres { get; }

        public TreePresentationTier Select(
            in TreePresentationInput tree,
            float3 cameraPositionMetres,
            TreePresentationTier previousTier = TreePresentationTier.Culled)
        {
            if (tree.Severed || tree.Health01 <= 0f)
                return TreePresentationTier.Culled;

            float distance = math.distance(cameraPositionMetres, tree.PositionMetres);
            float scaledDistance = distance / math.max(0.5f, tree.Scale);

            if (tree.Landmark)
            {
                float exit = previousTier == TreePresentationTier.Landmark
                    ? LandmarkExitMetres + HysteresisMetres
                    : LandmarkExitMetres;
                return scaledDistance <= exit ? TreePresentationTier.Landmark : TreePresentationTier.Culled;
            }

            if (Keeps(previousTier, TreePresentationTier.Full, scaledDistance, FullExitMetres))
                return TreePresentationTier.Full;
            if (scaledDistance <= FullExitMetres)
                return TreePresentationTier.Full;

            if (Keeps(previousTier, TreePresentationTier.Simplified, scaledDistance, SimplifiedExitMetres))
                return TreePresentationTier.Simplified;
            if (scaledDistance <= SimplifiedExitMetres)
                return TreePresentationTier.Simplified;

            if (Keeps(previousTier, TreePresentationTier.CanopyMember, scaledDistance, CanopyExitMetres))
                return TreePresentationTier.CanopyMember;
            return scaledDistance <= CanopyExitMetres
                ? TreePresentationTier.CanopyMember
                : TreePresentationTier.Culled;
        }

        private bool Keeps(TreePresentationTier previous, TreePresentationTier tier, float distance, float exit) =>
            previous == tier && distance <= exit + HysteresisMetres;
    }
}
