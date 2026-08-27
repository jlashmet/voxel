# CI operations

- `33043258034` / request `073f09096eb3a061f1a503e40061ddae9df501f7`: terminal failure. Batchmode test harness used `WaitForEndOfFrame`; replay artifact was diagnostic and visually rejected for tall dark bars.
- `33044213942` / request `8523bc98a3ddc857d1d937d59db50fb158e83993`: terminal success on source `1c02387f952e5eaef5845bafde12b73fcb9759f7`, but saved-pose image was manually rejected because the marked region still contained dark stalks. No queued/running request was replaced.
- `33044687964` / request `4814488bcd792ebd8f83439e463311f9666804e5`: final terminal success on exact source `6b05ee9db8157f7d26b1d343d210e4dbf15f51c8`. `ci/single-test=success`; focused PlayMode regression passed and original saved-camera replay completed.
