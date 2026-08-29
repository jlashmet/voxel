# CI operations

- `33230924543` / request `4424c2eaa328e573eea12a971a2c493b970a0f93`: admitted after queueing, completed `failure` on 2026-08-29. Focused test and real-player build both stopped on the same product compile error (`TopDownWorldLayout` missing namespace import in `KentridgeMacroWorldEvidenceDriver.cs`). Artifact `9708464190` uploaded diagnostics. No retry/replacement occurred while queued or running.
- Product fix: import `Game.WorldBuilder.Api` in the evidence driver (`339ca94f593653e84a02fe2d19712971bfd99e20`). Feature remains open pending a green repaired exact-SHA gate and built-player evidence.
