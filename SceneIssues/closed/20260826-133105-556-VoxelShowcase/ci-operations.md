# CI operations

- `66905ed3812838a19c0f46ad207c671434198ba8` — run `33110323331` failed Unity compilation because the new regression mixed compatibility `CastlePlan` with `CastleLayout`; test source corrected. No queued request replaced.
- `e6df5ac9c99efa2daff5fb78973318616e3d1534` — run `33118103870` compiled/replayed but the focused assertion found the expected retained leaf `Empty`; static reveal rejected and replaced by animation.
- `cb8dc99b7ffc83b6705244595c8d951e38988c92` — run `33123445937` failed compilation from ambiguous `Object`; regression aliased `UnityEngine.Object`.
- `c50248d94d033c6916f598582ff99f31e089f662` — run `33123891196` passed test/replay but cold Showcase bake consumed 3m25s and the five-minute job cap cancelled it; infrastructure diagnostic only.
- `8288cba1487e446754f6608ded6789e8ed252d21` — run `33129439694` passed test/replay, but human inspection rejected fallback-gray editor evidence; isolated presentation convergence rather than product behavior.
- `f94d89d1b8125400d06296470c84c6f9de9f144f` — run `33129981847` passed after a 12s settle but still produced noncanonical gray offscreen evidence; isolated view-dependent convergence.
- `425524222ee388d92fa4f880ce796300aa16539a` — run `33131101780` passed the behavioral regression and exact-pose replay; inspection showed the standalone replay still captured the pre-interaction closed gate, so evidence replay was made opt-in action-aware without changing gate behavior.
- `b531a60939828ef6102cc4fca3343cc305c74f05` — exact request for source `9222d8b9d2c7f50e285a224ac76669498b8a1276`. First attempt passed regression/build/45s player replay but a 214s cold bake caused cancellation and `ci/single-test=failure`; its artifact already showed both opened leaves. The one allowed infrastructure rerun reused the same request SHA/cache and completed `success`; `ci/single-test=success`, regression passed, player replay exited 0, and the final saved-pose frame visibly retains both opened leaves.
