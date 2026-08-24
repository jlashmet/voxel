# Hero arch look-development bench

Open `Assets/Scenes/ArchLookdev.unity` and enter Play Mode. Geometry sliders rebuild
automatically. Material, lighting, and lens controls update continuously.

## Comparing to the target

The focused, version-controlled target is `References/arch_reference.png`; the broader source
composition is retained at `References/sunlit-cleric-reference.png`. The in-scene split/overlay
controls were removed so reference pixels cannot obscure scene defects or leak into evidence.
Compare production-player captures externally. `ArchLookdevSceneTests` also publishes the focused
target beside its standalone-player screenshots for visual acceptance.

## Presets and captures

**Save preset** writes `Artifacts/ArchLookdev/hero-preset.json`; **Load preset** restores it.
**Capture** waits for production surface meshing to converge, then writes a PNG and a JSON
settings snapshot with matching names. A sweep writes five numbered PNG/JSON pairs, a 3×2
contact sheet, and an axis-value manifest while preserving and restoring the starting settings.

The available sweep axes are voussoir count, joint width, bevel, and moss coverage.

## Controlling a running scene

While the scene is playing, it publishes `Artifacts/ArchLookdev/state.json` and polls
`command.json` four times per second. The repository client writes valid commands:

```sh
tools/arch-lookdev.sh state
tools/arch-lookdev.sh capture candidate-name
tools/arch-lookdev.sh sweep voussoirs
tools/arch-lookdev.sh apply Artifacts/ArchLookdev/hero-preset.json
tools/arch-lookdev.sh save
tools/arch-lookdev.sh load
```

An `apply` command accepts either `state.json` or the complete settings object paired with a
capture. This keeps a candidate reproducible and avoids ambiguous partial parameter updates.

## Camera

- Right mouse: orbit
- Middle mouse: pan
- Wheel: dolly
- WASD and Q/E: move
- Shift: move faster
- F: frame the arch
- Tab: hide/show the bench
