# Required-CI teardown discriminator

## Exact failed evidence
Source `a67d64a8174104327a097f11183db772109d40e3`; transport `c72cb89cea7d8a25e10dc8e716eecb300e5702ab`; workflow run `34000107687`; artifact `9979710315`, SHA256 `ea8b83b779855c344269b26859f3b4ad8488b6040b702fafe4179724139f8d94`.

`ModuleValidation/Results/Tests/Persistent/persistent.log` records:
- line 143502: starts the second PlayMode phase;
- line 143944: its required case completes Passed;
- line 144017: starts the requested EditMode phase;
- lines 144029-144050: the preceding Test Runner executes `IPostBuildCleanup`, then fails `RestoreSceneManagerSetup` against the first PlayMode phase's deleted temporary scene;
- line 144077: the second temporary scene is also no longer a valid SceneAsset.

The artifact's final persistent summary is failed/exit 2. Passing individual cases and standalone screenshots do not turn that into a green required gate.

`VoxelCiPersistentTestRunner.OnRunFinished` advances phases through a single delayed callback. It has no contract that PlayMode's later scene cleanup is complete. Starting another run at that point permits overlapping ownership of temporary scene setup. Rather than guessing at private Unity state or adding timing sleeps, `run-module-validation.py` now routes PlayMode module phases and explicit PlayMode requests through separate existing `unity-run.sh` processes. EditMode batching, selected assemblies, all result checks and automatic player targets are retained.

## Behavioral regression and local result
Command: `python -m unittest discover -s tools/tests -p 'test_run_module_validation*.py' -v`.

The fixture invokes the real Python orchestration with one EditMode assembly, two independent PlayMode assemblies, a requested test, and a discovered player target. Only external subprocess execution is doubled. It asserts separate process invocations, unchanged selection, filter forwarding without an invented assembly, and rejection of zero-match, skipped, failed, missing and stale results or process failures.

Baseline runner blob `332ecc949991e40e9f29a145b25a9dac5052c59e`: 20 tests executed, failed with 14 failing assertions/subtests. The 12 pre-existing regressions passed. Repaired runner blob `da31b9b23614ca57c08a2641405480dceaa070c1`: all 20 tests passed, including 8 new isolation tests. Python compilation also passed. These are orchestration regressions, not Unity, graphics, performance, or visual acceptance results.

Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28` / run `34003328146` was already queued for source `de0aa1fb4221b06f8f63e6f22fc26ffba77defc8` when this independent repair was made. It has not been replaced. After it completes, the new source requires its own exact targeted CI and fresh built-player review before closure.
