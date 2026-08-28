# Experiment 001 — production module ownership

**Hypothesis:** Combat/Input production modules already exist and Kentridge only omitted scene wiring.

**Action / source:** On refreshed master `5465b893c141f2fa6a255a9e15a8c7929f740068`, probe `Assets/Game/Combat`, `Assets/Game/Input`, and the existing prototype/runtime ownership.

**Result:** Both production paths are absent; `Assets/CombatPrototype` remains. The playable slice therefore has no production `ICombatService` or input-context boundary available to compose.

**Verdict:** Rejected the scene-wiring-only hypothesis; supported a missing production migration seam.

**Next:** Add production Combat/Input API/runtime boundaries plus one Kentridge vertical slice and behavioral regression.
