# Temporary Placeholder Humanoids

This folder contains temporary third-party humanoid assets for character/NPC development while the generated-character pipeline is being built.

## Contents

- `Models/Male_Adult_01.fbx` — Microsoft Rocketbox adult male placeholder.
- `Models/Female_Adult_01.fbx` — Microsoft Rocketbox adult female placeholder.
- `Models/placeholder_male.characterfactory.json` — routes the male FBX through the normal Character Factory Unity importer.
- `Models/placeholder_female.characterfactory.json` — routes the female FBX through the normal Character Factory Unity importer.
- `Animations/Idle.fbx` — neutral breathing idle.
- `Animations/Walk.fbx` — neutral walking locomotion from Rocketbox's XY motion-extraction set.
- `Animations/Run.fbx` — neutral running locomotion from Rocketbox's XY motion-extraction set.
- `Animations/CrouchIdle.fbx` — crouched idle.
- `Animations/Wave.fbx` — simple wave emote.
- `Animations/Shrug.fbx` — simple shrug/interaction emote.
- `Editor/PlaceholderHumanoidImporter.cs` — imports all placeholder FBXs as Unity Humanoid so clips can retarget between the placeholders and future generated characters.
- `Licenses/` — upstream license text, pinned source revision/paths, and SHA-256 provenance.

The committed FBXs and animation files have committed Unity `.meta` files, so they are the stable temporary assets that can safely be referenced across checkouts.

## Character Factory integration

The two `*.characterfactory.json` descriptors are consumed by the existing `CharacterFactoryAssetImporter`. On Unity import they create/update these derived local assets:

- `Models/placeholder_male.prefab`
- `Models/placeholder_female.prefab`
- `PlaceholderCharacterParts.asset`

Those generated outputs and their `.meta` files are intentionally ignored by Git. They exercise the same Character Factory prefab/equipment shape as generated characters without making temporary generated wrappers part of the repository source of truth. Do not make committed scenes or Addressables depend on the generated wrapper GUIDs; use the committed model FBXs as the durable placeholder references until the generated-character pipeline owns stable character prefabs.

The generated local prefabs use the normal Character Factory shape: the imported skinned model is nested under a stable character root, an `Equipment` child is created, and `CharacterEquipmentController` is wired to the imported skeleton plus the shared generated part catalogue.

## Runtime fallback

`VoxelEngine.Characters.Runtime.CharacterVisualResolver` is the replacement seam for gameplay-facing character **visuals**. It has no dependency on Rocketbox or this third-party folder: it only resolves a preferred visual prefab and a fallback visual prefab.

For a temporary character or NPC:

1. Add `CharacterVisualResolver` to the character root.
2. Assign `Models/Male_Adult_01.fbx` or `Models/Female_Adult_01.fbx` to **Fallback Visual Prefab**. These committed FBXs have stable GUIDs and are safe for scene/prefab references.
3. Optionally assign a child transform to **Visual Root**; otherwise the component uses its own transform.
4. Leave **Preferred Visual Prefab** empty until a generated model is available.

At runtime, promote a generated model with:

```csharp
resolver.SetPreferredVisual(generatedCharacterPrefab);
```

The generated/preferred prefab immediately replaces the owned fallback instance. Setting the preferred visual back to `null` resolves the fallback again. The resolver only destroys/replaces the visual instance it owns, preserves unrelated character children, and normalizes the spawned visual to local position zero, identity rotation, and unit scale.

`CharacterVisualResolver` publishes `VisualChanged` whenever its owned visual changes. Generic runtime systems can use that signal to rebind visual-local components without knowing whether the visual came from Rocketbox, Character Factory, or another source.

`CharacterVisualResolver` deliberately does **not** rebind an external `CharacterEquipmentController` when the visual skeleton changes. Treat it as a visual replacement seam, not an equipped-character hot-swap API. Use the Character Factory wrapper when equipment/skeleton wiring is required, or let the generated-character lifecycle own equipment rebinding once that runtime contract exists.

Choosing male versus female remains an authoring/spawn-data decision outside the resolver; the runtime component deliberately has no placeholder-specific gender enum or asset path.

## Animation starter set

| Clip | Intended prototype use | Movement policy |
| --- | --- | --- |
| `Idle` | looping neutral idle | controller remains stationary |
| `Walk` | looping walk locomotion | controller-driven translation |
| `Run` | looping run locomotion | controller-driven translation |
| `CrouchIdle` | looping crouched idle | controller remains stationary |
| `Wave` | one-shot emote/interaction | no gameplay translation |
| `Shrug` | one-shot emote/interaction | no gameplay translation |

The animation FBXs are imported as Unity Humanoid clips and use semantic clip names (`Idle`, `Walk`, `Run`, `CrouchIdle`, `Wave`, `Shrug`). Idle/locomotion clips loop; Wave and Shrug are one-shot clips. They can be retargeted onto either placeholder body and later onto generated Humanoid characters without changing gameplay animation semantics.

Walk/Run use Rocketbox's XY motion-extraction source files, but the placeholder importer explicitly bakes horizontal displacement into the pose so every starter clip plays in-place. Unity 6000.5 also does not expose usable root-motion curves from these Humanoid imports. Gameplay locomotion therefore stays driven by the character motor/controller. A `PlayableGraph` test drives Walk on both placeholder avatars and verifies an actual retargeted leg-pose change.

### Runtime clip playback

`VoxelEngine.Characters.Runtime.CharacterAnimationPlayer` is the generic runtime playback seam. It plays any `AnimationClip` directly through a Unity `PlayableGraph`; it has no Rocketbox paths, clip-name table, or AnimatorController dependency.

Add it to the same character root as `CharacterVisualResolver`. It automatically binds to the current visual's child `Animator`. When the resolver swaps from a fallback visual to a preferred/generated visual, the player stops the graph bound to the old Animator and retargets itself to the new visual's Animator.

Gameplay remains responsible for deciding **which** semantic clip should play:

```csharp
animationPlayer.Play(idleClip);
animationPlayer.Play(walkClip);
animationPlayer.Play(waveClip);
animationPlayer.Stop();
```

That keeps locomotion/emote policy outside the temporary asset package. A later gameplay animation policy can choose Idle/Walk/Run from movement state and use the same player after the Rocketbox placeholders are removed.

No temporary Animator state machine is defined here.

## Intended use

These assets are deliberately isolated under `Assets/ThirdParty/PlaceholderHumanoids` so they can be removed when generated character models are ready. Gameplay code should depend on Unity Humanoid/Animator contracts, not on Rocketbox-specific bone names.

The two Rocketbox models intentionally omit the large legacy TGA texture/material set. During import, every skinned material slot is instead bound to the active render pipeline's default 3D material (URP in this project), so the bodies remain visible as neutral development mannequins without adding disposable texture weight. They are visual/rig placeholders, not final art.

The animation set is intentionally small and named by gameplay purpose instead of importing Rocketbox's full animation library. Add clips only when a prototype actually needs them.

## Mixamo note

Mixamo downloads require an authenticated Adobe/Mixamo session, so this repository does not copy Mixamo binaries from unofficial mirrors. These Rocketbox animations fill the temporary locomotion/emote role and use the same avatar family as the placeholder bodies. If authenticated Mixamo FBXs are added later, place them under `Animations/Mixamo/`; the importer will treat them as Humanoid as well.

## Source

- Microsoft Rocketbox Avatar Library, revision `0943055db6ec570bcef9f2c8b41c9e5467c808f9`: `https://github.com/microsoft/Microsoft-Rocketbox`

See `Licenses/THIRD_PARTY_NOTICES.md` for exact upstream paths and licenses.