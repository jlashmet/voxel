# 21 Gameplay audio integration & semantic cue presentation — implementation plan

**Target module:** `Assets/Game/Audio/Api` / `Runtime` (`Game.Audio.Api`, `Game.Audio.Runtime`).

## API

Semantic `AudioCueRef`, cue request/event with semantic origin (CharacterId/WorldObjectId/world point as appropriate), one-shot identity/dedupe metadata, and sustained audio-state descriptors. No AudioClip/AudioSource references in gameplay APIs.

## Runtime

1. Map semantic cues to Unity audio assets/presentation configuration locally.
2. Subscribe to confirmed gameplay/cutscene semantic events; keep prediction/anticipation explicitly local and deduped.
3. Resolve semantic origins to current presentation transforms outside authority.
4. Reconstruct sustained audio from current state; do not replay historical one-shots on reconnect/restore.
5. Route existing cutscene sound cues through the same playback service without double-playing.
6. Bind volume/preferences from #23 settings.

## Dependencies

Semantic APIs/events from gameplay modules; #23 preferences; presentation-side object resolution.

## Tests / proof

Cue mapping, unknown cue handling, dedupe, reconnect behavior, cutscene/gameplay shared service, headless gameplay unchanged when audio absent.

## Do not build

No gameplay decisions based on audio playback, clip ids in domain events, or scene-local substitute AudioSources.
