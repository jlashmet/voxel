# CI operations

- 2026-08-30T10:28Z — final request transport `d33869e84f033b95b74f050e135343859453fb24` admitted as run `33306559976`, then failed in **Resolve test request** before Unity. Cause: `replay_seconds` was encoded as string `"60"`; workflow contract requires an integer from 20 to 60. Requested PlayMode test and built-player capture were skipped, so this run satisfies no validation gate. Correct the request value to integer `60` on the same `ci-test/fixes/agent-5` transport; do not change production code or create another transport.
