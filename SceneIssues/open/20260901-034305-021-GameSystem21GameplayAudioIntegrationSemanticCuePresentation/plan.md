# 21 Gameplay audio integration & semantic cue presentation — implementation plan

**Target module:** `Assets/Game/Audio/Api` / `Runtime` (`Game.Audio.Api`, `Game.Audio.Runtime`).

## Acceptance / observed baseline

Audio is client-local presentation only: gameplay/cutscenes publish semantic identity/current state, Audio maps that to Unity playback, and missing/failed audio never changes authority. Baseline had no `Assets/Game/Audio` owner or indexed production `AudioSource`; Kentridge authored `door.open` but its presentation handoff was a no-op. User audio preferences belong to System23; Audio must not create competing settings authority.

## Architecture / approach

1. **Selected:** engine-neutral `AudioCueRef`, stable one-shot event identity, semantic origins and sustained-state descriptors in `Game.Audio.Api`; Unity mapping, playback, mix, dedupe, origin resolution and sustained reconciliation in `Game.Audio.Runtime`. Kentridge composition owns one `KentridgeGameplayAudioIntegration` shared by cutscene and confirmed gameplay cues.
2. **Selected preference seam:** System23 owns `IUserPreferencesStore`, `UserPreferences` lifecycle/persistence and invokes `IAudioPreferencesSink` on boot-load and settings changes. `Game.Audio.Runtime.AudioUserPreferencesSink` implements that API port and maps only `MasterVolume` into the existing `AudioPresentationRuntime` mix, preserving Audio-owned SFX/music/ambience/voice gains.
3. **Rejected:** gameplay/scene code choosing clips or calling `AudioSource`, Audio persisting user settings, Application.Runtime becoming Audio's playback owner, or separate cutscene/gameplay Audio runtimes.

`Assets/Game/Audio/Validation/AudioValidation.unity` is the module-owned built-player surface. Production Kentridge routes authored `door.open` through `SlicePresentation -> ICutsceneSoundCueRuntime -> KentridgeGameplayAudioIntegration -> AudioPresentationRuntime`, mapping to `world.door.opened`.

## Ownership / blast radius

`Game.Audio.Api` remains engine-neutral and independent of Application. `Game.Audio.Runtime` references only `Game.Application.Api` for the semantic preferences port; it does not depend on Application.Runtime or persistence. Gameplay authority remains unchanged. The preference adapter changes local playback gain only and cannot alter semantic events, sustained state, session state, or gameplay outcomes.

## Current state / remaining gates

Previous feature behavior passed exact run `33882411311`; master-synced production SHA `25e9d072ee2b6f923a17260dcc9a9a81361d25df` passed exact request `cb3c694eb3f24219d423aae64513b2dc01e9a77e`, run `33888505678`, on attempt 2 after an attempt-1 native Unity crash was retried as infrastructure.

System23 landed on `origin/master` at `68171d79250ab4ec44c9581df4dd827e8b5b3d92` and was merged into this feature at `337c9620cf7bc9937b8cb86e13d1e79bf3a3e052`. The real `UserPreferences` / `IAudioPreferencesSink` seam is now bound in Audio.Runtime, with focused EditMode coverage and module-player validation requiring `master=0.42` while preserving bus mix.

**Remaining gate:** run a new exact-SHA targeted CI request for the current integrated production head. On green, complete T21-016/T21-032, populate closure fields, move only this SceneIssue `open -> closed`, then promote by PR + auto-merge.
