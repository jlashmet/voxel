# Temporary Placeholder Humanoids

This folder contains temporary third-party humanoid assets for character/NPC development while the generated-character pipeline is being built.

## Contents

- `Models/Male_Adult_01.fbx` — Microsoft Rocketbox adult male placeholder.
- `Models/Female_Adult_01.fbx` — Microsoft Rocketbox adult female placeholder.
- `Animations/KayKit_Knight_AnimationLibrary.fbx` — KayKit Adventurers Knight FBX used as a compact humanoid animation library. The source character pack contains 75 animations.
- `Editor/PlaceholderHumanoidImporter.cs` — forces these FBXs to Unity Humanoid so animation clips can be retargeted between the placeholders and future generated characters.
- `Licenses/` — upstream license texts and source/provenance notes.

## Intended use

These assets are deliberately isolated under `Assets/ThirdParty/PlaceholderHumanoids` so they can be removed when generated character models are ready. Gameplay code should depend on Unity Humanoid/Animator contracts, not on Rocketbox or KayKit-specific bone names.

The two Rocketbox models are intentionally imported without their large legacy TGA texture set. They are geometry/rig placeholders, not final art. This keeps the temporary package small and makes the replacement boundary obvious.

## Mixamo note

Adobe Mixamo animations can be used in games, but downloading them requires an authenticated Adobe/Mixamo session. This repository therefore does not contain copied Mixamo binaries from an unofficial mirror. The KayKit library provides redistributable CC0 humanoid animations for the same temporary retargeting role. If authenticated Mixamo FBXs are added later, place them under `Animations/Mixamo/`; the importer will treat them as Humanoid as well.

## Sources

- Microsoft Rocketbox Avatar Library: `https://github.com/microsoft/Microsoft-Rocketbox`
- KayKit Adventurers: `https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0`

See `Licenses/THIRD_PARTY_NOTICES.md` for exact upstream paths and licenses.
