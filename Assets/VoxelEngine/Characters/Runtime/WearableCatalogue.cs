using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Runtime lookup table for independent wearable assets.
    /// The public API intentionally exposes identifiers rather than this catalogue.
    /// </summary>
    [CreateAssetMenu(fileName = "WearableCatalogue", menuName = "Voxel Engine/Characters/Wearable Catalogue")]
    public sealed class WearableCatalogue : ScriptableObject
    {
        [SerializeField] private List<WearableAsset> entries = new List<WearableAsset>();

        private Dictionary<string, WearableAsset> index;

        public bool TryGet(string partId, out WearableAsset asset)
        {
            EnsureIndex();
            return index.TryGetValue(partId, out asset);
        }

        private void OnValidate()
        {
            index = null;
        }

        private void EnsureIndex()
        {
            if (index != null)
            {
                return;
            }

            index = new Dictionary<string, WearableAsset>(System.StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                WearableAsset entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.PartId))
                {
                    continue;
                }

                if (index.ContainsKey(entry.PartId))
                {
                    Debug.LogError($"Duplicate character part id '{entry.PartId}' in {name}.", this);
                    continue;
                }

                index.Add(entry.PartId, entry);
            }
        }
    }
}
