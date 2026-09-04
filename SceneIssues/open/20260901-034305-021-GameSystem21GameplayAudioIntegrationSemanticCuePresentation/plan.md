# 21 Gameplay audio integration & semantic cue presentation — implementation plan

**Target module:** `Assets/Game/Audio/Api` / `Runtime` (`Game.Audio.Api`, `Game.Audio.Runtime`).

## Acceptance / observed baseline

Audio is client-local presentation only: gameplay/cutscenes publish semantic identity/current state, Audio maps that to Unity playback, and missing/failed audio never changes authority. Baseline had no `Assets/Game/Audio` owner or indexed production `AudioSource`; Kentridge authored `door.open` but its presentation handoff was a no-op. User preferences remain Application-owned.

## Architecture / approach

1. **Selected:** engine-neutral semantic cue/event/current-state contracts in `Game.Audio.Api`; Unity playback, mix, dedupe and sustained reconciliation in `Game.Audio.Runtime`. `KentridgeGameplayAudioIntegration` shares one runtime across cutscene and confirmed gameplay cues.
2. **Preferences:** System23 now owns `UserPreferences`, persistence, and `IAudioPreferencesSink` lifecycle. Audio.Runtime implements that port by mapping `MasterVolume` into `AudioPresentationRuntime.ApplyMix` while retaining Audio-owned SFX/music/ambience/voice gains.
3. **Rejected:** gameplay choosing clips/`AudioSource`, Audio persisting settings, or parallel cutscene/gameplay audio services.

`Assets/Game/Audio/Validation/AudioValidation.unity` is the module-owned built-player surface. Production Kentridge routes authored `door.open` through `SlicePresentation -> ICutsceneSoundCueRuntime -> KentridgeGameplayAudioIntegration -> AudioPresentationRuntime`, mapping to `world.door.opened`.

## Ownership / blast radius

`Game.Audio.Api` stays engine-neutral. `Game.Audio.Runtime` depends on `Game.Application.Api` only for the preference port and does not own persistence. Gameplay authority remains unchanged. Unknown cue/origin/backend failures are presentation-only. The Kentridge integration preserves current HUD/input/progression composition from master.

## Current state / remaining gates

Earlier production behavior passed exact run `33888505678` on SHA `25e9d072ee2b6f923a17260dcc9a9a81361d25df` (attempt 2; attempt 1 was a native Unity infrastructure crash).

System23 landed on master as `68171d79250ab4ec44c9581df4dd827e8b5b3d92`; the canonical preference seam is now bound. First preference exact request `6fec1bd337275cb9e186250489dba82e7c0ccd07` for production `82f658a56aa7a352411ec123564853cf0c76bb65` failed in run `33922351473` because Audio test/validation asmdefs lacked direct `Game.Input.Api` references required by `UserPreferences`' public `InputBindingOverride` signature. The product fix is commits `3180937b93a21f58c85f5f03fcc1dc316d0475cd` and `0163f9092aa30856de82ec33ee9226af0dbf6cee`.

Current master `2749a5133319eb5cf5019d821bb00ee3e2fe1a4e` adds System17 HUD/input/progression Kentridge composition. Merge it while preserving those semantic changes plus GameSystem21 audio forwarding, then run a new exact-SHA targeted CI request. T21-016 and T21-032 remain incomplete until that integrated production head is green; after green, close and promote through PR + auto-merge.
