# Third-party notices — temporary placeholder humanoids

These assets are temporary development placeholders and are isolated so they can be removed when the generated-character pipeline is ready.

## Microsoft Rocketbox Avatar Library

- Upstream: `https://github.com/microsoft/Microsoft-Rocketbox`
- Upstream revision used: `0943055db6ec570bcef9f2c8b41c9e5467c808f9`
- License: MIT (upstream `LICENSE.md`, copied into this folder during import)
- Avatar files used:
  - `Assets/Avatars/Adults/Male_Adult_01/Export/Male_Adult_01.fbx` -> `../Models/Male_Adult_01.fbx`
  - `Assets/Avatars/Adults/Female_Adult_01/Export/Female_Adult_01.fbx` -> `../Models/Female_Adult_01.fbx`
- Animation files used:
  - `Assets/Animations/all_animations_max_motextr_static/m_idle_breathe_01.max.fbx` -> `../Animations/Idle.fbx`
  - `Assets/Animations/all_animations_max_motextr_xy/m_walk_neutral.max.fbx` -> `../Animations/Walk.fbx`
  - `Assets/Animations/all_animations_max_motextr_xy/m_run_neutral.max.fbx` -> `../Animations/Run.fbx`
  - `Assets/Animations/all_animations_max_motextr_static/m_crouch_idle.max.fbx` -> `../Animations/CrouchIdle.fbx`
  - `Assets/Animations/all_animations_max_motextr_static/m_wave_01.max.fbx` -> `../Animations/Wave.fbx`
  - `Assets/Animations/all_animations_max_motextr_static/m_gestic_shrug_01.max.fbx` -> `../Animations/Shrug.fbx`

Walk and Run come from Rocketbox's XY motion-extraction variants; the other starter clips use static variants. The Unity 6000.5 Humanoid imports used here retarget and play correctly, but Walk/Run do not report Unity `AnimationClip.hasMotionCurves`, so the placeholder package does not make a root-motion guarantee and expects gameplay translation to remain controller-driven.

`SOURCE_SHA256SUMS.txt` records the SHA-256 hashes captured for the exact downloaded FBX binaries used by this placeholder package.
