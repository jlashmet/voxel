# Experiment 013 — direct-driver evidence remediation

## Hypothesis
A separate validation-only runtime helper can safely reorder Rossdam lake before Rossdam settlement, provide settlement-overview framing, and preserve the Moordell player-height road-arrival screenshot while leaving the proven macro evidence driver unchanged.

## Exact run / discriminator
Run `33293664047`, transport `2fef75fe4a8f1a6e2d86dcf691765cc2f1246357`, exact source `72c5986b71d79473a1d7725b9d65085cf520509a`, artifact `9726759390`.

The focused editor result is infrastructure-red rather than a managed product assertion: `single.xml` is absent and the editor terminates natively in the Burst child domain under `Burst.Compiler.IL.Server.EntryPointMethodGrouper`. The built player succeeds with zero harness assertions, so its runtime/evidence behavior remains usable as the discriminator.

## Observation
The intended helper behavior did not execute. The player has no `target-order=lake-before-rossdam` log and still schedules `Moordell -> Rossdam -> lake`; Fairy only becomes content-ready near the 60-second cutoff and Fairy/Orc/ridge/network captures are missing. Full-resolution `macro-moordell.png` and `macro-rossdam.png` each frame only one generic blockout. The Moordell road-arrival capture also races `LateUpdate`: `CaptureScreenshot` queues the write, but clearing road-arrival mode in the same update lets the survey camera replace the player-height camera before the queued frame is rendered.

## Decision
Rejected. Do not add replay time or relax readiness/visual acceptance. Move target ordering and settlement framing directly into the already-proven `KentridgeMacroWorldEvidenceDriver`, keep `_moordellRoadArrivalPending` active through the queued screenshot frame, and remove the competing helper. Production streaming radius, CharacterMotor, renderer/device budgets, topology, geography semantics, and the supported 60-second CI replay cap remain unchanged.

The next exact request must prove the direct-driver code actually logs lake-before-Rossdam ordering, captures all seven macro targets plus the player-height road arrival inside 60 seconds, and visibly shows four blockouts in every generic settlement survey before this experiment is considered remediated.