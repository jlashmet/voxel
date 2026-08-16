# Temporary Placeholder Humanoids

This folder contains temporary third-party humanoid assets for character/NPC development while the generated-character pipeline is being built.

## Contents

- `Models/Male_Adult_01.fbx` — Microsoft Rocketbox adult male placeholder.
- `Models/Female_Adult_01.fbx` — Microsoft Rocketbox adult female placeholder.
- `Models/placeholder_male.characterfactory.json` — routes the male FBX through the normal Character Factory Unity importer.
- `Models/placeholder_female.characterfactory.json` — routes the female FBX through the normal Character Factory Unity importer.
- `Models/placeholder_male.prefab` — generated Character Factory male prefab with a committed Unity GUID.
- `Models/placeholder_female.prefab` — generated Character Factory female prefab with a committed Unity GUID.
- `PlaceholderCharacterParts.asset` — shared Character Factory part catalogue used by both placeholder prefabs.
- `Animations/Idle.fbx` — neutral breathing idle.
- `Animations/Walk.fbx` — neutral walking locomotion with translated root motion from Rocketbox's XY extraction set.
- `Animations/Run.fbx` — neutral running locomotion with translated root motion from Rocketbox's XY extraction set.
- `Animations/CrouchIdle.fbx` — crouched idle.
- `Animations/Wave.fbx` — simple wave emote.
- `Animations/Shrug.fbx` — simple shrug/interaction emote.
- `Editor/PlaceholderHumanoidImporter.cs` — forces all placeholder FBXs to Unity Humanoid so clips can retarget between the placeholders and future generated characters.
- `Licenses/` — upstream license text, exact source paths, and SHA-256 provenance.

## Using the placeholders

The two `*.characterfactory.json` descriptors are consumed by the existing `CharacterFactoryAssetImporter`, which creates/updates:

- `Models/placeholder_male.prefab`
- `Models/placeholder_female.prefab`
- `PlaceholderCharacterParts.asset`

The descriptors and Rocketbox FBXs remain the reproducible source inputs, but these three Unity outputs and their `.meta` files are committed as well. That gives scenes, NPC definitions, Addressables, and other serialized Unity assets stable GUIDs when they reference the temporary character prefabs. Reimporting a descriptor intentionally regenerates the corresponding prefab through the same Character Factory path.

The generated prefabs use the normal Character Factory character-prefab shape: the imported skinned model is nested under a stable character root, an `Equipment` child is created, and `CharacterEquipmentController` is wired to the imported skeleton plus the shared part catalogue. Prototype gameplay should use these prefabs instead of depending directly on Rocketbox bone names or FBX hierarchy details.

The animation FBXs are imported as Unity Humanoid clips. They can therefore be retargeted onto either placeholder prefab and later onto generated Humanoid characters without changing gameplay animation semantics.

## Animation starter set

| Clip | Intended prototype use | Root motion |
| --- | --- | --- |
| `Idle` | looping neutral idle | no translated locomotion |
| `Walk` | looping walk locomotion | XY translation available |
| `Run` | looping run locomotion | XY translation available |
| `CrouchIdle` | looping crouched idle | no translated locomotion |
| `Wave` | one-shot emote/interaction | not intended for locomotion |
| `Shrug` | one-shot emote/interaction | not intended for locomotion |

No temporary Animator state machine is defined here. Gameplay should consume these through the project's existing Unity Humanoid/Animator seam so a future generated-character implementation can replace the Rocketbox bodies and clips without changing actor logic.

## Intended use

These assets are deliberately isolated under `Assets/ThirdParty/PlaceholderHumanoids` so they can be removed when generated character models are ready. Gameplay code should depend on Unity Humanoid/Animator and the Character Factory prefab/equipment contracts, not on Rocketbox-specific bone names.

The two Rocketbox models are intentionally imported without their large legacy TGA texture set. They are geometry/rig placeholders, not final art. This keeps the temporary package small and makes the replacement boundary obvious.

The animation set is intentionally small and named by gameplay purpose instead of importing Rocketbox's full animation library. Add clips only when a prototype actually needs them.

## Mixamo note

Mixamo downloads require an authenticated Adobe/Mixamo session, so this repository does not copy Mixamo binaries from unofficial mirrors. These Rocketbox animations fill the temporary locomotion/emote role and use the same avatar family as the placeholder bodies. If authenticated Mixamo FBXs are added later, place them under `Animations/Mixamo/`; the importer will treat them as Humanoid as well.

## Source

- Microsoft Rocketbox Avatar Library: `https://github.com/microsoft/Microsoft-Rocketbox`

See `Licenses/THIRD_PARTY_NOTICES.md` for exact upstream paths and licenses.