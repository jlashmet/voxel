# Temporary Placeholder Humanoids

This folder contains temporary third-party humanoid assets for character/NPC development while the generated-character pipeline is being built.

## Contents

- `Models/Male_Adult_01.fbx` — Microsoft Rocketbox adult male placeholder.
- `Models/Female_Adult_01.fbx` — Microsoft Rocketbox adult female placeholder.
- `Models/placeholder_male.characterfactory.json` — routes the male FBX through the normal Character Factory Unity importer.
- `Models/placeholder_female.characterfactory.json` — routes the female FBX through the normal Character Factory Unity importer.
- `Animations/Idle.fbx` — neutral breathing idle.
- `Animations/Walk.fbx` — neutral walking locomotion with translated root motion from Rocketbox's XY extraction set.
- `Animations/Run.fbx` — neutral running locomotion with translated root motion from Rocketbox's XY extraction set.
- `Animations/CrouchIdle.fbx` — crouched idle.
- `Animations/Wave.fbx` — simple wave emote.
- `Animations/Shrug.fbx` — simple shrug/interaction emote.
- `Editor/PlaceholderHumanoidImporter.cs` — imports all placeholder FBXs as Unity Humanoid so clips can retarget between the placeholders and future generated characters.
- `Licenses/` — upstream license text, exact source paths, and SHA-256 provenance.

The committed FBXs and animation files have committed Unity `.meta` files, so they are the stable temporary assets that can safely be referenced across checkouts.

## Character Factory integration

The two `*.characterfactory.json` descriptors are consumed by the existing `CharacterFactoryAssetImporter`. On Unity import they create/update these derived local assets:

- `Models/placeholder_male.prefab`
- `Models/placeholder_female.prefab`
- `PlaceholderCharacterParts.asset`

Those generated outputs and their `.meta` files are intentionally ignored by Git. They exercise the same Character Factory prefab/equipment shape as generated characters without making temporary generated wrappers part of the repository source of truth. Do not make committed scenes or Addressables depend on the generated wrapper GUIDs; use the committed model FBXs as the durable placeholder references until the generated-character pipeline owns stable character prefabs.

The generated local prefabs use the normal Character Factory shape: the imported skinned model is nested under a stable character root, an `Equipment` child is created, and `CharacterEquipmentController` is wired to the imported skeleton plus the shared generated part catalogue.

## Animation starter set

| Clip | Intended prototype use | Root motion |
| --- | --- | --- |
| `Idle` | looping neutral idle | no translated locomotion |
| `Walk` | looping walk locomotion | XY translation available |
| `Run` | looping run locomotion | XY translation available |
| `CrouchIdle` | looping crouched idle | no translated locomotion |
| `Wave` | one-shot emote/interaction | not intended for locomotion |
| `Shrug` | one-shot emote/interaction | not intended for locomotion |

The animation FBXs are imported as Unity Humanoid clips and use semantic clip names (`Idle`, `Walk`, `Run`, `CrouchIdle`, `Wave`, `Shrug`). Idle/locomotion clips loop; Wave and Shrug are one-shot clips. They can be retargeted onto either placeholder body and later onto generated Humanoid characters without changing gameplay animation semantics.

No temporary Animator state machine is defined here. Gameplay should consume these through the project's existing Unity Humanoid/Animator seam. Root motion remains opt-in at the gameplay Animator/controller layer.

## Intended use

These assets are deliberately isolated under `Assets/ThirdParty/PlaceholderHumanoids` so they can be removed when generated character models are ready. Gameplay code should depend on Unity Humanoid/Animator contracts, not on Rocketbox-specific bone names.

The two Rocketbox models are intentionally imported without their large legacy TGA texture set. They are geometry/rig placeholders, not final art. This keeps the temporary package small and makes the replacement boundary obvious.

The animation set is intentionally small and named by gameplay purpose instead of importing Rocketbox's full animation library. Add clips only when a prototype actually needs them.

## Mixamo note

Mixamo downloads require an authenticated Adobe/Mixamo session, so this repository does not copy Mixamo binaries from unofficial mirrors. These Rocketbox animations fill the temporary locomotion/emote role and use the same avatar family as the placeholder bodies. If authenticated Mixamo FBXs are added later, place them under `Animations/Mixamo/`; the importer will treat them as Humanoid as well.

## Source

- Microsoft Rocketbox Avatar Library: `https://github.com/microsoft/Microsoft-Rocketbox`

See `Licenses/THIRD_PARTY_NOTICES.md` for exact upstream paths and licenses.
