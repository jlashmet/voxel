using System;
using Game.Characters.Api;
using UnityEngine;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Scene-specific composition handoff for gameplay-character authority. The registry remains
    /// Game.Characters-owned; this MonoBehaviour only lets independently installed Kentridge
    /// composition adapters share the same API instance without static/global state.
    /// </summary>
    public sealed class KentridgeCharacterRegistryAnchor : MonoBehaviour
    {
        private const string PlayerCameraName = "Kentridge Player Camera";

        public ICharacterRegistry Characters { get; private set; }

        public static void AttachToPlayerRoot(ICharacterRegistry characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));

            GameObject root = GameObject.Find(PlayerCameraName);
            if (root == null) return; // Headless/unit composition may have no player scene root.

            KentridgeCharacterRegistryAnchor anchor =
                root.GetComponent<KentridgeCharacterRegistryAnchor>() ??
                root.AddComponent<KentridgeCharacterRegistryAnchor>();
            if (anchor.Characters != null && !ReferenceEquals(anchor.Characters, characters))
                throw new InvalidOperationException(
                    "Kentridge player root already owns a different gameplay character registry.");
            anchor.Characters = characters;
        }
    }
}
