# Experiment 020 — Replay diagnostic activation

## Trigger
Exact feature run `33639437668` tested source `46c10a5505fb80709c3b7d294ed66ff8cea27f6b`. The runner's preflight `Wait for Unity` failed because an unrelated interactive Unity editor and AssetImportWorker under `/Users/jlashmet/code/voxel` remained alive. Automatic module validation therefore did not execute. The standalone SceneIssue step still built the real player successfully (peak 8413 MB, 90 s) and replayed the Mountain Dragon route.

The player again reached grounded `resolved-49` and timed out at waypoint index 51 before `mid-turn`, reproducing experiment 019's target symptom. However, the player log contained no `WAYPOINT_REPLAY diagnostic` samples even though `ShowcaseWaypointReplayDiagnostics.cs` was included in the player compilation. Therefore run `33639437668` cannot discriminate hard collision, boundary deflection, grounding loss, or replay steering.

## Minimal activation defect
The replay harness and diagnostic previously relied on separate `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` callbacks. The harness callback demonstrably ran, armed the 95-waypoint route, and drove the player. The diagnostic callback produced neither activation nor sample output. The smallest reliable seam is to attach the read-only diagnostic from the already-active harness immediately after the harness has bound its production `CharacterMotor` and route.

## Repair
- Remove the diagnostic's independent runtime-initialize/command-line activation path.
- Add `ShowcaseWaypointReplayDiagnostics.AttachTo(root, replay)` from the successful replay setup path.
- Bind the observer directly to that replay instance and emit `WAYPOINT_REPLAY diagnostic activated`.
- Preserve the existing once-per-second telemetry fields: waypoint index/name/target, motor feet position, horizontal distance, one-second displacement, grounded state.

No AutoWalk, yaw, speed, sprint multiplier, gravity, collision, road geometry, waypoint coordinates, arrival radii, vertical predicates, or timeout behavior changes.

## Falsifier
The next exact built-player run must contain `WAYPOINT_REPLAY diagnostic activated` followed by periodic diagnostic samples. If those lines are still absent, do not alter traversal behavior; isolate player initialization/linking before another movement or geometry change. If samples appear, classify the repeated `resolved-49 -> mid-turn` stall using experiment 019's decision rule before any production repair.
