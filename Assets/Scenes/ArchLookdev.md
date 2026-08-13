# Hero arch look-development bench

Open `Assets/Scenes/ArchLookdev.unity` and enter Play Mode. Geometry sliders rebuild
automatically. Material, lighting, lens, and reference controls update continuously.

## Comparing to the target

The scene loads `~/Downloads/Sunlit Cleric by the Waterfall.png`, falling back to
`Artifacts/ArchLookdev/target.png`. Reference modes are:

- **Split** — live render on the left and reference on the right.
- **Overlay** — reference composited over the live render with adjustable opacity.
- **Target** — reference only.
- **Off** — live render only.

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
