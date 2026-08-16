using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Characters.Runtime
{
    /// <summary>
    /// Runtime lookup table for independently generated character parts.
    /// The public API intentionally exposes identifiers rather than this catalogue.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterPartCatalogue", menuName = "Voxel Engine/Characters/Character Part Catalogue")]
    public sealed class CharacterPartCatalogue : ScriptableObject
    {
        [SerializeField] private List<CharacterPartAsset> entries = new List<CharacterPartAsset>();

        private Dictionary<string, CharacterPartAsset> index;

        public bool TryGet(string partId, out CharacterPartAsset asset)
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

            index = new Dictionary<string, CharacterPartAsset>(System.StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                CharacterPartAsset entry = entries[i];
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
