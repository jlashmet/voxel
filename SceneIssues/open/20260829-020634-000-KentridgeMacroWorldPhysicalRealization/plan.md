# Plan

## Contract and ownership
`issue.json` remains the contract for resumed assignment `20260829-020634-000-KentridgeMacroWorldPhysicalRealization`; do not create a replacement SceneIssue. Preserve the source-backed Mounting Force macro graph and deliver physical settlements, contiguous terrain-aware routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded runtime cost.

Affected validation ownership is WorldBuilder macro physical output, Showcase presentation readiness, and Kentridge Playable macro-world integration. The reconciled branch has no Rendering production diff, so do not revive Agent 1's retired GPU-relocation work; Kentridge acceptance must exercise the current production renderer instead.

## Exact evidence — 2026-09-08
Current reconciliation base remains `origin/master=6e34db751a76e61708d70edebb6bf7af6fe94658` until the next pre-promotion fetch.

Exact source `e97cc09d6c8ef170472b2bab8ab4b1d61fd5bc4e`, request `adc5f001ff35bf8f9c8bb792e4fa0505dbc7aa16`, run `34184870430`, established that selected tests and the requested macro PlayMode regression pass, and that the focused Kentridge player reaches real Application New Game, local CharacterMotor traversal, Moordell, macro-road CharacterMotor traversal, and Moordell road-arrival evidence. Its remaining module failure was strict presentation convergence while CI forced the retired CPU fallback; Rossdam content itself became settled. The module scenario now requires the current production GPU cutover.

Exact source `d5e495c125c43c841516f1083550e5af33a8d4a0`, request `2bcb078b7757bdc3365837f92a8f32411d377b0e`, run `34188460121`, had an infrastructure-only persistent-editor failure: Unity/Mono segfaulted inside Burst `LibraryCompiler.GetAotCompilerOptions` before any test summary or C# compile/test failure existed. The same run's standalone SceneIssue player completed cleanly for 180 s and showed healthy current-GPU coverage (`missingVisible=0`, no in-flight coverage work), but remained at `FrontEnd/MainMenu`.

Exact source `e43ae0356e562de9eb153753a520899db8b895df`, request `a1b72d214a7d3bb44b30b840d6e8c0f8190fd3aa`, run `34190241434`, validates the hidden-object lifecycle correction: the DontSave discovery regression is green and both the focused module player and the full SceneIssue replay now request New Game, execute local + macro-road CharacterMotor traversal, and capture Moordell/road-arrival evidence without runtime exceptions. Both then reach `content-ready target=rossdam` but not Rossdam capture or the final network overview before 180 s.

The repeated Rossdam publication symptom produced a new exact discriminator: experiment 047's previously selected close-oblique settlement validation composition was dormant. Exact logs contained no `close-survey` event and still used the legacy 70 m near-nadir settlement target; source inspection showed the dedicated module bootstrap omitted the helper, SceneIssue profile discovery was incomplete, hidden evidence discovery used the wrong API, and the close demand was applied only after production streaming.

Source `5bfe34d5fdab75e1b714fa120c93da122feb1472` reactivated that existing correction without changing renderer acceptance. Exact request `3c301837eb5f0048e5079a3e1b5c1de9d439ae65`, run `34192047180`, proves all persistent EditMode assemblies pass and the explicitly requested macro PlayMode regression passes. The remaining module-player failure is validation code, not world generation or renderer infrastructure: the close-survey helper reflects `EvidenceTarget.Label` and `FocusDm` as fields even though the evidence target exposes them as read-only properties. Its first Moordell frame throws `InvalidOperationException`; because the type was cached before the invalid accessors were validated, later frames produce repeated `NullReferenceException`s in both the focused module player and SceneIssue replay. Consequently no `close-survey` event is emitted and the player falls back to the legacy 70 m path.

Selected fix: keep the same close-survey composition and replace the incorrect field access with validated `PropertyInfo` access. Cache the target type only after both `Label:string` and `FocusDm:Int2` properties resolve, so a metadata mismatch cannot poison later frames. Do not change renderer code, radius, generation, budget, concurrency, or coverage thresholds.

## Remaining gates
1. Re-run exact-SHA validation after the target-property fix. Require `close-survey target=rossdam`, no runtime exceptions, real CharacterMotor traversal, and `capture-ready target=macro-network-overview` in the focused module player.
2. Require the 180-second SceneIssue replay to reach all settlement/geography/network/traversal evidence without exceptions. If Rossdam strict coverage still fails with the close survey demonstrably active, isolate that new exact state before another production change.
3. Inspect full-resolution durable captures for the four blockout settlements, roads, Rossdam lake/detour, Southern Ridge/pass, differentiated terrain, and CharacterMotor traversal; only the issue's explicit blockout-quality allowance may lower the normal art bar.
4. Record final convergence/FPS/CPU/GPU/streaming/memory evidence against existing budgets without changing those budgets.
5. Complete every checklist item, move only this issue `open -> closed` with fixed metadata, recheck/merge current master if it advances, then open `fixes/agent-6 -> master`, enable auto-merge, and monitor the required PR gate until merged.
