# Experiment 006 — player material compatibility

## Question
Did the exact green run produce acceptable bandit presentation in the built player, or only behavioral correctness?

## Evidence
- Exact request `102b9a1929be1ab485304ab76bb6640ea44af15f`, run `33133220597`, passed the PlayMode regression and 60 s real-player capture.
- Opened native 1928×836 `verification-final.png`: all three bandits were present and grounded in the PineForest, but runtime primitive hood/belt/weapon gear rendered bright magenta.
- The rigged character itself rendered normally, isolating the defect to `GameObject.CreatePrimitive`'s built-in material under the project's URP player.

## Discriminator / action
Reject promotion despite green CI. Reuse the rigged character's shipped material/shader for runtime gear and extend the regression to prove gear inherits that shader; rerun exact-SHA CI and visually inspect the new replay.
