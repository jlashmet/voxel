# Experiment 003 — active authoring hierarchy

## Hypothesis
The mismatch comes from weak composition under the pinned capture camera: uniform limestone clutter overwhelms a path represented only by sparse pavers.

## Action
On the current active `TerrainLookdev.cs`, reduce incidental rock/turf density, make the route a continuous tapered ground ribbon with restrained cobbles, and keep five stronger outcrop groups spanning near/mid/far depth. The current master production/test blobs match the exact pre-change baselines used for this candidate, so no newer TerrainLookdev work is displaced.

## Expected falsifier
Reject the candidate if the exact captured-camera regression or full replay still lacks a readable route/depth hierarchy, or if new intersections/artifacts appear.

## Verdict
Selected for fresh exact-SHA targeted CI and replay; final visual evidence remains the promotion gate.
