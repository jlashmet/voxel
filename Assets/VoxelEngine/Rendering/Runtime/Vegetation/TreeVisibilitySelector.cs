using System;
using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    public readonly struct SelectedTreePresentation
    {
        public SelectedTreePresentation(
            ulong stableId,
            int sourceIndex,
            TreePresentationTier tier,
            TreeVisibilityEntry visibility)
        {
            StableId = stableId;
            SourceIndex = sourceIndex;
            Tier = tier;
            Visibility = visibility;
        }

        public ulong StableId { get; }
        public int SourceIndex { get; }
        public TreePresentationTier Tier { get; }
        public TreeVisibilityEntry Visibility { get; }
    }

    /// <summary>
    /// Stateful presentation selector only. Tree placement/damage remain owned by the vegetation
    /// world; this class remembers the previous visual tier solely to apply hysteresis.
    /// </summary>
    public sealed class TreeVisibilitySelector
    {
        private readonly Dictionary<ulong, TreePresentationTier> _previousTiers = new();
        private readonly HashSet<ulong> _seenThisQuery = new();

        public void Select(
            IReadOnlyList<TreeVisibilityEntry> trees,
            float3 cameraPositionMetres,
            in TreeVisibilityTierPolicy policy,
            Func<TreeVisibilityEntry, bool> isLandmark,
            List<SelectedTreePresentation> individuals,
            List<TreeVisibilityEntry> canopyMembers)
        {
            if (trees == null) throw new ArgumentNullException(nameof(trees));
            if (individuals == null) throw new ArgumentNullException(nameof(individuals));
            if (canopyMembers == null) throw new ArgumentNullException(nameof(canopyMembers));

            individuals.Clear();
            canopyMembers.Clear();
            _seenThisQuery.Clear();

            for (int i = 0; i < trees.Count; i++)
            {
                TreeVisibilityEntry tree = trees[i];
                _seenThisQuery.Add(tree.StableId);
                _previousTiers.TryGetValue(tree.StableId, out TreePresentationTier previous);

                var input = new TreePresentationInput(
                    tree.StableId,
                    tree.Instance.PositionMetres,
                    tree.Instance.Scale,
                    tree.Damage.FoliageHealth,
                    tree.Damage.Severed,
                    isLandmark != null && isLandmark(tree));

                TreePresentationTier tier = policy.Select(in input, cameraPositionMetres, previous);
                _previousTiers[tree.StableId] = tier;

                switch (tier)
                {
                    case TreePresentationTier.Full:
                    case TreePresentationTier.Simplified:
                    case TreePresentationTier.Landmark:
                        individuals.Add(new SelectedTreePresentation(
                            tree.StableId, tree.SourceIndex, tier, tree));
                        break;
                    case TreePresentationTier.CanopyMember:
                        canopyMembers.Add(tree);
                        break;
                }
            }

            if (_previousTiers.Count == _seenThisQuery.Count) return;
            var staleIds = new List<ulong>();
            foreach (KeyValuePair<ulong, TreePresentationTier> pair in _previousTiers)
                if (!_seenThisQuery.Contains(pair.Key)) staleIds.Add(pair.Key);
            for (int i = 0; i < staleIds.Count; i++)
                _previousTiers.Remove(staleIds[i]);
        }

        public void Reset() => _previousTiers.Clear();
    }
}
