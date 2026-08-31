# Experiment 004 — exact-CI successful-bake fast exit

## Discriminator
Exact request `b9fdc14c3c25...` / run `33302337781` exercised the repaired sparse fixed-structure bake path. The result artifact shows the source bake completed and persisted/imported a `200 regions, 18.2 MiB` startup image (`18,136,152`-byte payload plus `504,058`-byte manifest), then logged `[ShowcaseWorldBaker] Completed in-run bake cost gate(s); requesting CI batch shutdown.` Unity entered normal graceful teardown with return code 0 but did not return to the external 240 s wrapper before it was killed. The requested focused test was therefore skipped.

## Hypothesis
The remaining failure is process teardown latency after successful persistence, not voxel generation or sparse structure semantics. `EditorApplication.Exit(0)` requests Unity's full graceful shutdown and can consume the remaining wrapper budget even after the bake is already durably complete.

## Selected change
Keep the existing strict policy gate: only `GITHUB_ACTIONS=true`, Unity batch mode, and exact `-executeMethod VoxelEngine.Showcase.Editor.ShowcaseWorldBaker.BakeShowcaseWorld` may use the fast successful termination. After all world disposal, capture-suppression restoration, payload/manifest writes, imports, saves, and success logging are complete, terminate the successful process immediately instead of waiting for Unity's full graceful teardown.

Interactive editor bakes, local/non-CI batch invocations, `GITHUB_ACTIONS=false`, `-runTests`, other execute methods, generic batch processes, runtime streaming, voxel composition, startup coverage, and the workflow's 240 s / 14 GB contracts must remain unchanged.

## Regression requirement
Extend the editor-policy regression so the process-exit action is injectable/testable: the exact CI bake success context requests exit code 0 only after successful completion, while every ordinary/negative context retains normal teardown and never invokes the immediate process exit.

## Acceptance of experiment
The next exact-parent CI request must return successfully from the bake subprocess inside 240 s, reopen Unity against the source-matched generated payload, run the exact Mountain Dragon final acceptance green, and complete built-player replay/capture evidence. Until then the timing/provenance/closure gates remain open.
