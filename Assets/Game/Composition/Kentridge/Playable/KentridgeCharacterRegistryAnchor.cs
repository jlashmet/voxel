using System;
using Game.Characters.Api;
using Game.Composition.Kentridge.Runtime;
using UnityEngine;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Scene-specific composition handoff for gameplay-character authority and the optional Kentridge
    /// session-extension factory. The authoritative services remain module-owned; this MonoBehaviour
    /// only lets independently installed Kentridge adapters join the one SessionOrchestration graph
    /// without static/global service state.
    /// </summary>
    public sealed class KentridgeCharacterRegistryAnchor : MonoBehaviour
    {
        private const string PlayerCameraName = "Kentridge Player Camera";

        public ICharacterRegistry Characters { get; private set; }
        public IKentridgeSessionRuntimeExtensionFactory SessionRuntimeExtensionFactory { get; private set; }

        public static void AttachToPlayerRoot(ICharacterRegistry characters)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));

            KentridgeCharacterRegistryAnchor anchor = FindOrCreatePlayerAnchor();
            if (anchor == null) return; // Headless/unit composition may have no player scene root.
            if (anchor.Characters != null && !ReferenceEquals(anchor.Characters, characters))
                throw new InvalidOperationException(
                    "Kentridge player root already owns a different gameplay character registry.");
            anchor.Characters = characters;
        }

        public static void AttachSessionRuntimeExtensionFactory(
            IKentridgeSessionRuntimeExtensionFactory extensionFactory)
        {
            if (extensionFactory == null) throw new ArgumentNullException(nameof(extensionFactory));

            KentridgeCharacterRegistryAnchor anchor = FindOrCreatePlayerAnchor();
            if (anchor == null) return;
            if (anchor.SessionRuntimeExtensionFactory != null &&
                !ReferenceEquals(anchor.SessionRuntimeExtensionFactory, extensionFactory))
                throw new InvalidOperationException(
                    "Kentridge player root already owns a different session runtime extension factory.");
            anchor.SessionRuntimeExtensionFactory = extensionFactory;
        }

        public static IKentridgeSessionRuntimeExtensionFactory ResolveSessionRuntimeExtensionFactory()
        {
            GameObject root = GameObject.Find(PlayerCameraName);
            if (root == null) return null;
            KentridgeCharacterRegistryAnchor anchor = root.GetComponent<KentridgeCharacterRegistryAnchor>();
            return anchor == null ? null : anchor.SessionRuntimeExtensionFactory;
        }

        private static KentridgeCharacterRegistryAnchor FindOrCreatePlayerAnchor()
        {
            GameObject root = GameObject.Find(PlayerCameraName);
            if (root == null) return null;
            return root.GetComponent<KentridgeCharacterRegistryAnchor>() ??
                   root.AddComponent<KentridgeCharacterRegistryAnchor>();
        }
    }
}
