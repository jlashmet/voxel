# Experiment 003 — active authoring hierarchy

## Hypothesis
The mismatch comes from weak composition under the pinned capture camera: uniform limestone clutter overwhelms a path represented only by sparse pavers.

## Action
On source `d841d3461d3ee7a763414cb6ad35bed69afafbf4`, reduce incidental rock/turf density, make the route a continuous tapered ground ribbon with restrained cobbles, and keep five stronger outcrop groups spanning near/mid/far depth. Current master production/test blobs matched the exact pre-change baselines, so no newer TerrainLookdev work was displaced.

## Falsifier
Reject if the exact captured-camera regression or full replay lacks a readable route/depth hierarchy, or if new intersections/artifacts appear.

## Result
Exact request `fae0bdd57d4b5401f396647c4da082cd68461c19` passed `ci/single-test` in run `33138598594`. The focused PlayMode regression passed, and the 60-second real-player replay pinned the original camera at `(-0.70, 18.80, -18.50)`, FOV 29 while capturing the revised scene.

## Verdict
Confirmed. Promote the assigned capture through pending/closed bookkeeping; no further product or CI change is required.
