# Experiment 005 — graded access production attempt 2, exact-view failure

**Hypothesis** — The large foreground stair in the saved VoxelShowcase view was the shallow `lower-west-neighbourhood-access` route. That route climbs only 11 dm, so compiling it as a six-tread stone flight with stepped cheek walls could be creating an oversized stair monument. Shallow Lower Ward access should grade continuously while deeper Market/Upper/Civic access remains stepped.

**Attempt** — Added a focused circulation regression and changed `KentridgeUrbanAccessCatalogue` so shallow Lower Ward access emitted a stone ramp instead of discrete stair treads. The exact saved camera was replayed from the resulting `fixes` head.

**Exact-view evidence** — Actions run `32836898382`, source `4e57f60a4953ae05a52bbb7c2dcdc9e3c718f11b`, completed successfully and produced artifact `9559052756` (`scene-220516-attempt-2-exact-view`), digest `sha256:a26d4b54e188f68c806eeb1ea1d257a79e62c176ff636e03fd41938c6d42d211`. The replay verified the frozen SceneIssue pose.

**Result** — Rejected. Attempt 1 and attempt 2 replay frames were compared pixel-for-pixel at the same 1364×767 replay resolution. Every changed pixel was confined to the top-left FPS text; the rendered world geometry was identical. The supposed access stair therefore was not contributing to the captured geometry.

**Rollback** — The speculative regression and production change were fully removed after the failed visual gate. Regression rollback commit: `f90e117034494b852534317b43474ba485ae2b45`. Production rollback commit: `e135475db3d209163bae88486029f14b4ad4bbb4`; `KentridgeUrbanAccessCatalogue.cs` returned to blob `dfb55df181919db57eddcd5c41003f02c4eb5a9e`.

**Conclusion** — Do not spend another attempt on `KentridgeUrbanAccess` without final-catalogue evidence. The third production attempt must be preceded by enumeration of the actual surviving definitions intersecting the captured lower-town corridor.
