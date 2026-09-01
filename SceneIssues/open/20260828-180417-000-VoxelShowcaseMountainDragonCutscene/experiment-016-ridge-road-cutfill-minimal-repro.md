# Experiment 016 — Ridge / road cut-fill minimal repro

## Trigger
The production Mountain Dragon ascent failed the same acceptance symptom after two materially different route-control revisions:

- coarse 13-control spiral: 60 dm required cut/fill vs 42 dm allowed;
- denser 25-control spiral: 50 dm required cut/fill vs 42 dm allowed in run `33469216133`.

Per the issue workflow, no third geometry tweak was attempted until isolating the root cause.

## Minimal reproduction
I reproduced the current `MountainLandformSurface` integer mass construction and `WorldRoadResolver` routing/grading math using the exact Mountain Dragon semantic inputs, but replaced the outside-world fallback terrain with a flat surface at the mountain origin height. This removes Showcase base-terrain variation while retaining the authored natural mountain, spiral controls, road profile, A* spacing, and grade/cut-fill behavior.

The failure persists without base terrain. The first reproduced violation occurs on authored spiral leg 9 where the route crosses generated secondary ridge mass `ridge6.1`: terrain reaches roughly 101 dm above the mountain origin while grade smoothing can carry only roughly 51 dm at that point, producing the same 50 dm cut/fill excess seen by the built player. Additional violations recur where the spiral crosses other selected radial ridge masses (`ridge8.1`, `ridge10.1`, etc.).

The authored controls themselves land on multiple elevated ridge sectors, so increasing angular sampling cannot eliminate the problem: a winding road around this radial-ridge family necessarily crosses those ridges.

## Competing hypotheses

1. **Base terrain causes the failure.** Rejected: the failure persists with flat fallback terrain.
2. **The shared resolver ignores its grade/cut-fill contract.** Rejected: the resolver correctly rejects the route during its grading phase; the failure is the expected enforcement of the 42 dm contract.
3. **The coarse spiral undersamples the mountain.** Rejected as sole cause: doubling controls reduced the first observed excess from 60 dm to 50 dm but repeated ridge-crossing failures remain.
4. **Showcase selected ridge relief that is too strong for its own road profile.** Supported: parameter discrimination resolves the isolated route without changing shared road behavior or acceptance constraints.

## Parameter discrimination
Keeping the following unchanged:

- `MountainMacroShape.Ridged`;
- six ridge directions;
- roughness amplitude 24 dm;
- the same 1.5-turn / 25-control spiral;
- 280 permille maximum grade;
- 42 dm maximum cut/fill;
- 20 dm resolver sampling;

produced these isolated worst cut/fill results as only `RidgeStrengthPermille` changed:

- 620: repeated failure (>42 dm; isolated worst case substantially above the contract)
- 400: failure (~48 dm)
- 360: failure (~45 dm)
- 340: resolves (~38 dm)
- 320: resolves (~38 dm)
- 300: resolves (~34 dm)
- 280: resolves (~34 dm)

A value of 300 retains a visibly ridged semantic mountain while leaving useful margin beneath the unchanged road cut/fill contract. This is Showcase composition policy, not a shared API change.

## Root cause / next fix
`RidgeStrengthPermille = 620` creates secondary radial frusta whose local relief is incompatible with the authored winding road's unchanged 42 dm cut/fill budget. The correct next fix is to reduce the Mountain Dragon composition's ridge-strength parameter to 300 and retain all shared road constraints and ownership boundaries. Exact-source CI and built-player evidence must still validate that result; this experiment is diagnostic evidence, not acceptance closure.
