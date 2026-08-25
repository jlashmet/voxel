# Experiment 004 — Integration realization handoff

## Hypothesis

If the backend-facing WorldBuilderWorldGen integration assembly wraps the two legacy realization-fact interfaces behind a Game/integration-owned public type, then Kentridge composition can remain free of `MountingForce` assembly references while `CreateSession` exposes only Game-owned types. Strengthening the regression to inspect every public bootstrap method should prove the leak is closed rather than merely compiling by restoring a legacy reference.

## What performed + source commit

Starting from feature tip `83274f4845aa090c39f41f85f7718be281b63cd6`. Plan: inspect `CreateSession` consumers, strengthen the ownership regression, add the smallest integration-owned realization handoff, migrate `CreateSession`/callers, then repin `ci-test/fixes/agent-1` and rerun the exact ownership regression.

## Result

Pending.

## What learned

Experiment 003’s second compile failure is an architectural signal: commit `d6191178d3b242eeac747700204845eb87dcfe01` deliberately removed legacy worldgen references from Kentridge runtime. Re-adding them would regress the intended boundary. The remaining realization-fact interfaces are backend-neutral semantically but physically live in the legacy backend namespace, so they belong behind the explicit WorldBuilderWorldGen integration layer rather than on the Kentridge composition public surface.

## Next

Inspect all `KentridgeCampaignSessionBootstrap.CreateSession` consumers and implement the smallest wrapper/overload that keeps Kentridge composition backend-blind, then rerun the exact focused regression.
