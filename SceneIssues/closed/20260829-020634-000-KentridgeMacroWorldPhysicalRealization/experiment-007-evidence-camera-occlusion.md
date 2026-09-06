# Experiment 007 — evidence camera occlusion and remaining window

## Hypothesis
The remaining visual failure is evidence composition, not missing physical content: generic settlement buildings are placed at four deterministic ±190 dm offsets from each settlement center, while the current validation camera stays at terrain height and aims at the center. Regional terrain can therefore occlude the nearest building. The lake camera is similarly too low/far to show its full authored extent. Ridge/overview are absent because gameplay control begins near t=40s and the current sequence starts ridge at t=58.3s.

## Action / source
Inspected exact source `cd194a16d32adb442f9d2f699b1ffc1c00f661ee`, request `0a7a8ca5fe1cff7142cab71c7bf89772809e1123`, run `33261299347`, artifact `9717362552`, and `TopDownWorldPhysicalPlanner.BuildGenericSettlement`.

## Result
Focused test and built player are green. `macro-moordell.png` visibly contains a grounded roof/wall blockout after prewarm removal. Rossdam/Fairy/Orc frames have coverage=True but are ground-level views dominated by intervening macro terrain. Planner source confirms each generic settlement has four buildings centered at (±190, ±190) dm from the settlement center. The Rossdam-lake frame shows the route response but only a narrow water sliver. Run timing captures Moordell ~t47, Rossdam ~t50, lake ~t52, Fairy ~t55, Orc ~t58, then starts ridge; ridge and overview cannot finish by t60.

## Verdict
Use generated geometry to compose evidence: elevate generic-settlement views and focus a real building/settlement footprint; elevate the lake view according to region extent while retaining the constrained route in frame. Do not alter settlement/world generation. Increase only the validation-profile opening time scale and trim nonessential evidence dwell/traversal overhead while preserving normal-time CharacterMotor movement and coverage-gated captures.

## Next step
Implement generated-plan-derived evidence poses and tighter validation timing, then run one exact-SHA request and require visible settlement blockouts, substantial lake, ridge/pass, network overview, and road traversal.
