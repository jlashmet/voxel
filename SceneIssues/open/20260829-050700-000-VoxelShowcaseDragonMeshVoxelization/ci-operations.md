# CI operations

- Persistent transport: `ci-test/fixes/agent-1`.
- No final request issued yet.
- Do not modify `.github/test-request.json` on `fixes/agent-1`.
- Do not replace queued/running CI or create another transport.
- Branch refresh-merged current `origin/master` `5865c6e04f93c7d2ba0f10258909f38115424607` at merge commit `040bc7a146672c01e9c7f9f1d165d65b3c5854e6`; current feature head after independent required work is based on that master and is not behind it.
- 2026-08-30 source-transfer discriminator: shell GitHub access cannot resolve `github.com`; the GitHub connector rejects/omits oversized binary payload transfer; the separate direct downloader requires a successfully web-viewed binary URL, while GitHub/raw PLY web fetches return cache-miss and therefore cannot authorize that download. Microsoft metadata confirms the intended exact `Mesh008.ply` source object (`fa8316f8b1c698da1d539b04cb83992437439dc6`, 4,386,778 bytes), and the PLY is `binary_little_endian`, ruling out lossless UTF-8 line reconstruction. Per the repeated-gate rule, do not retry these same transfer methods without a genuinely new transport capability.
- Live `VoxelShowcase.cs` call-site wiring is also blocked in this execution environment: the connector only replaces whole UTF-8 files, while reads of the 58 KB scene owner truncate. The reusable selection/input-consumption seam and focused regressions are committed, but the live call sites remain unchecked rather than risking unrelated source loss or adding a competing controller.
- Exact-SHA targeted/built-player validation remains pending until the selected source bytes, baked dragon, dragon-specific regressions, final live showcase wiring/evidence, and final current-master refresh are complete. Do not spend the assigned CI request on the incomplete artifact state.
