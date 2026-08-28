# Experiment 004 — verification presentation convergence

## Question
Was the gray `verification-final.png` from green run `33129439694` evidence that the animated gate fix failed, or only that clean evidence was captured before presentation convergence?

## Discriminator
Compare temporal evidence from the same exact-SHA run. The authoritative PlayMode regression passed all closed/mid/open voxel assertions and the real-player scene-issue replay completed. At the saved camera pose, the real player was fallback gray at 14.6 s but showed the authored timber/iron/gold gate by 24.6 s. The editor artifact had been captured immediately after castle storage completion plus only 20 uncapped frames.

## Result
Gate-state failure is falsified by the passed production-path assertions. The changing gray-to-materialized replay at a fixed pose confirms presentation convergence lag. Twenty frame yields were not a meaningful settle under uncapped PlayMode.

## Action / falsifier
Keep production unchanged. Target the exact saved `Showcase Camera`, wait 12 wall-clock seconds after the final gate mutation, then render the clean 1928x836 evidence. Reject this adjustment if the next artifact remains fallback gray or does not make both retained opened leaves and clear centre passage judgeable.
