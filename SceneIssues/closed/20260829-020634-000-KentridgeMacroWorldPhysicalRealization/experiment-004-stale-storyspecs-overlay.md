# Experiment 004 — stale StorySpecs overlay

**Hypothesis:** final CI run `33258816868` failed because current master was independently broken by the opening-story feature.

**Action / source:** inspect the final agent-6 diff against `master` after request source `afc684712447cf000996c310aa41e8e967fb5dc0`; compare `Assets/Game/WorldBuilder/Api/StorySpecs.cs` on feature vs master and correlate with artifact `9716641348` compiler errors.

**Result:** falsified the baseline hypothesis. Agent-6 still carried stale `StorySpecs.cs` blob `0c399134ff696ef65e74f7cf7df27be03d778280`, while master has `3b34ab066a11c8af3f003a0559db3a4ea5357bba`. The stale blob removed `CutsceneCompletedConditionSpec`, `JoinPartyMemberEffectSpec`, `GrantSpellEffectSpec`, plus matching `StoryCondition` / `StoryEffect` factories—the exact symbols reported missing by CI. `StoryRuleEngine.cs` itself matched master, explaining why the failure initially looked external.

**Verdict:** agent-6 product/merge defect. Restore `StorySpecs.cs` exactly from current master; do not alter the opening-story implementation.

**Fix:** `eb6b77cc13bf5b1850a81df9a73a6250f7d2ba5b` restores master blob `3b34ab066a11c8af3f003a0559db3a4ea5357bba`.

**Next:** verify `StorySpecs.cs` disappears from the feature diff, refresh current master, then issue a fresh exact-SHA final targeted request.
