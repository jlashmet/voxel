# CI operations

- `66905ed3812838a19c0f46ad207c671434198ba8` — final request for feature `6c4e149839a1205c6111929fd13c192a27ae7b04`; admitted as run `33110323331`, queued until runner availability, then failed during Unity compilation. Root cause: new regression mixed compatibility `VoxelEngine.Showcase.CastlePlan` with `Game.Structures.Api.CastleLayout`; replay build failed from the same compiler error. Product/test source corrected on `fixes/agent-6`; no queued request was replaced.
