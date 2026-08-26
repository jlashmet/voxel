# CI operations

- The final integrated EditMode request was published once as `3bc7f76ac452f737f50d1643465cb1df2781936b`, sourcing feature commit `f31a9c2c02f25c7e21cc0a0447a9e765947ddeee`.
- Exact run `33003343182` remained queued while shared self-hosted runner work completed, including Showcase Performance runs `33002880787` and `33002941583` plus intervening master workflow activity.
- The known queued run was left untouched; no replacement, retry branch, no-op commit, custom workflow, or second CI-ref update was issued.
- Once admitted, the requested test ran and completed successfully. Final `ci/single-test` status is `success`.
- Subsequent master changes through `78fa0ea38baeb4a68b13c66fb4927d62fad00b71` were CI/process cleanup plus unrelated SceneIssue bookkeeping, not glazing production/test or captured-scene inputs. They were reconciled into `fixes/agent-3` before the final saved-camera replay.
