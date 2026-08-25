# Experiment 016 — campaign world suite

## Hypothesis

The reservation-filtered combined catalogue still realizes the opening campaign's named site,
hidden space, NPC placements, cutscene bindings, secret, and reachable destination correctly.

## What was performed

Against source `138623f3e6976a5905ec7e965325d93028bec4bc` plus the production/test diff, ran the full
`VoxelEngine.Tests.EditMode.KentridgeCampaignWorldRealizationTests` fixture through
`tools/unity-run.sh`.

## Result

`verification-campaign-world-results.xml` records exactly 3 tests executed, 3 passed, 0 failed in
0.144 seconds. The guarded Unity process exited 0; its log is
`verification-campaign-world-unity.log`.

## What was learned

The hypothesis is confirmed. Named gameplay sites and their realization facts remain intact after
secondary catalogue placement filtering.

## Next

Run semantic-landmark coverage for the same combined catalogue.
