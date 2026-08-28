# Experiment 001 — producer routing

**Hypothesis:** terrain replication fails because the live showcase impact bypasses the authoritative multiplayer request path.

**Action / source:** inspected the assigned full-frame split capture metadata and the live producer at `c1419aa4c42b528322cad545ebc5a52d46d73ab6`; compared surviving networking code with commit `41a004da1c29aad4872b39ea851d54d30d9a410a`.

**Result:** `VoxelShowcase.StepTornadoes` directly invokes `_world.Explode`. The historical diff explicitly removed the active-session `TryRequestExplosion` branch, movement pump, session construction, and network UI. `ShowcaseMultiplayerSession` still sends alteration requests, runs host authority, and applies authoritative events. Lower-level two-player/UTP/event/convergence tests remain in the suite.

**Verdict:** confirmed producer bypass. A relay/applier change is not justified because the missing request is earlier than those layers.

**Next:** restore scene-scoped session composition and authoritative impact routing; protect it with a production-router behavioral regression.
