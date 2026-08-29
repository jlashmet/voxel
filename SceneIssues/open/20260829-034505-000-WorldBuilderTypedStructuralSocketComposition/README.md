# Typed Structural Socket Composition

This feature is a shared WorldBuilder/structure-authoring foundation. Its purpose is to let large and reusable voxel structures be assembled from compatible, independently bounded pieces through deterministic typed sockets rather than monolithic generators or scene-local coordinates.

The required proving cases are:

1. Monumental bridge spanning a multi-region mountain gorge with a river below.
2. Castle assembly using reusable wall, tower, and gatehouse pieces.
3. Multi-level cliffside settlement/platform chain anchored to steep terrain.
4. Meso-scale building attachment showing facade/roof sockets without misusing sockets for every micro-detail.

See `issue.json` for the full contract, negative cases, visual acceptance, and performance/streaming requirements.