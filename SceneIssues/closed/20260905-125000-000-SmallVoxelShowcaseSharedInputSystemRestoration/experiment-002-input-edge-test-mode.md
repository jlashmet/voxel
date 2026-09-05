# Experiment 002 — Input edge regression test mode

## Hypotheses

1. **Synthetic device ownership:** the persistent Editor already owns `Keyboard.current` / `Mouse.current`, so queued state is written to non-current devices.
2. **Input frame/update ownership:** the regression is running in an EditMode assembly even though it asserts `wasPressedThisFrame` semantics that require a player-loop-compatible Input System test runtime.

## Discriminators

- Exact-SHA run `33984299208` made no explicit Structures request and still failed only the three new `ShowcaseInputSystemTests`; the actual built `SmallVoxelShowcase` input replay passed. This isolated the failure to the regression harness rather than production input or the independent Structures bootstrap.
- Making the synthetic keyboard/mouse current did **not** change the same three failing edge assertions in exact-SHA run `33986010080`.
- Switching the EditMode fixture to `InputSettings.UpdateMode.ProcessEventsManually` did **not** change those same three assertions in exact-SHA run `33987455658`; the built `SmallVoxelShowcase` replay again passed.

## Root cause

Unity Input System's `InputTestFixture` implementation states that it severs the Input System from native/editor runtime state, restores a known state per test, and is designed for PlayMode tests; EditMode is generally unsupported. The package ships that fixture from assembly `Unity.InputSystem.TestFramework`. The repository regression lived in `VoxelEngine.Showcase.Tests.EditMode`, so it asserted frame-edge input semantics in the unsupported execution mode.

This satisfied the issue-guide stop condition after two materially different harness fixes: the remaining failure was not another device/timing guess. The direct Input System edge regression belonged in a module-owned PlayMode test assembly using `InputTestFixture`.

## Selected correction

Moved only `ShowcaseInputSystemTests` to `Tests/PlayMode/VoxelEngine.Showcase.Tests.PlayMode`, derived it from `InputTestFixture`, preserved the test asset GUID, and gave the production assembly friend access to that test assembly. Other Showcase EditMode tests and production `ShowcaseInputSystem` stayed unchanged.

## Verdict

**Confirmed.** Exact feature SHA `7e6c609c34dff4768032f9046e891f43cbd935b7`, transport `e60a1c7c6e348d9876b35baa8b4a5898b7043abe`, workflow `33988857330` passed the repository-derived Showcase EditMode + PlayMode module validation, explicit Structures PlayMode regression, module-local player validation, Kentridge integration, and actual built `SmallVoxelShowcase` replay. The PlayMode ownership change removed the repeated edge-state failure without changing production input behavior.
