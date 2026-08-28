# Experiment 003 — exact-CI compiler discriminator

**Hypothesis:** The first exact final request failed because the new production Combat/Input modules do not compile.

**Action / source:** Inspect run `33131843444` / request `fa782d338872cf053bb3aab78f9e47abd70e4b8d`, including `single.log` and the real-player build log.

**Result:** Both compiles report only `CS0619` at `KentridgeCombatEncounterTests.cs` lines 38 and 51: Unity 6000.5 deprecates `Object.GetInstanceID()` as an error. No new production file has a compiler error, and the requested test never executed.

**Verdict:** Rejected a production-module compile defect; confirmed a test-only compatibility defect.

**Next:** Preserve the actor-persistence invariant with `Is.SameAs(leadBandit)`, then request fresh exact-SHA CI for the corrected source state.
