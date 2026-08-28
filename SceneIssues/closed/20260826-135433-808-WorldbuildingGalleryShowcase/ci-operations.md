# CI operations

- Source branch: `fixes/agent-1`
- Exact tested source SHA: `a3b0c60880dcc0731c6b9f900c13d9a72e51d91c`
- Transport branch: `ci-test/fixes/agent-1`
- Transport SHA: `0a4e06f8f40d143bc2197cb1addb6ad0fd9d18b4`
- The transport has the tested source SHA as its sole parent; its transport-only change is `.github/test-request.json`.
- Targeted workflow run `33214102360` / job `98993762977`: `success`, including the requested test and real-player visual capture.
- No additional CI transport was created or queued.
