# Experiment 020 — opening PlayMode memory guard

## Hypothesis

The full `KentridgeOpeningVerticalSlicePlayTests` fixture can run safely within the Unity wrapper's
system-memory floor after the EditMode validation sequence.

## What was performed

Attempted the full PlayMode fixture through `tools/unity-run.sh` against source
`138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, requesting results in
`verification-opening-vertical-slice-results.xml` and a log in
`verification-opening-vertical-slice-unity.log`.

## Result

The hypothesis is inconclusive. Unity began loading the PlayMode test run, but the wrapper killed
the process before any test result was produced when system free memory fell to 7,934 MB, below
the binding 8,192 MB safety floor. The wrapper exited 7 and left no live Unity editor process.

## What was learned

This is a host-memory safety stop, not a test failure. The guard must not be bypassed, and no claim
can be made about this fixture from this attempt. The already completed production-player replays
remain valid runtime evidence, but the PlayMode suite should be retried only after memory recovers.

## Next

Check system headroom without mutating other processes; retry only when free plus inactive memory
is safely above the wrapper floor.
