using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountingForce.Game.Composition.CharacterEquipment
{
    [CreateAssetMenu(
        fileName = "CharacterPartCatalogue",
        menuName = "Mounting Force/Characters/Part Catalogue")]
    public sealed class CharacterPartCatalogue : ScriptableObject
    {
        [SerializeField] private List<CharacterPartDefinition> parts =
            new List<CharacterPartDefinition>();

        public int Count => parts != null ? parts.Count : 0;

        public void Configure(params CharacterPartDefinition[] definitions)
        {
            parts = definitions != null
                ? new List<CharacterPartDefinition>(definitions)
                : new List<CharacterPartDefinition>();
        }

        public bool TryGetPart(string partId, out CharacterPartDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(partId) || parts == null)
            {
                return false;
            }

            for (int i = 0; i < parts.Count; i++)
            {
                CharacterPartDefinition candidate = parts[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.PartId))
                {
                    continue;
                }

                if (string.Equals(candidate.PartId, partId, StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
