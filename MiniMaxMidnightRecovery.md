# Midnight selection fix and recovery — 2026-11-26

## Cause

`MiniMaxForm.CheckDayChanged()` incorrectly cleared `IsChecked` (Run selection),
wrote an empty checked-profiles.json, and rebuilt the selected-only table. Chrome
profiles were not deleted; MiniMax's selection was erased.

## Correct behavior

- Midnight resets **IsClaimed only**, in memory and claimed-profiles.json.
- Run selections and checked-profiles.json remain unchanged.
- Displayed profiles survive the day rollover and subsequent save/restart.
- Date rollover alone does not cancel a running refresh-all or check-in.
- The earlier test expecting midnight selection clearing was corrected.

## Recovered selections

Restored 20 entries in
`%LOCALAPPDATA%\ChromeProfileLauncher\checked-profiles.json`.
18 match current MiniMax Global 9Router accounts and saved UUID mappings. The
earlier 20-account 9Router backup identifies the two original missing selections:

- Profile 78 — yuzz.cezh10@gmail.com
- Profile 79 — yuzz.cezh11@gmail.com

Current Chrome metadata was backed up and validated before restoration. MiniMax
was closed before writing the selection to avoid its empty in-memory state
overwriting the recovered file. No 9Router account/token records were changed.

Private pre-recovery state and profile metadata backup:
`Backups\MidnightRecovery-20261126-001209\`.

## Verification

- Actual main-window UI: 20 displayed rows, including cezh10 and cezh11.
- Five new midnight tests exercise the real reset owner: 20 rows retained, no
  checked-file writes, active token not cancelled, only claims cleared, and
  selections intact after save/reload.
- Full focused suite: 127 passing tests.
- Existing check-in simulation: 9/9.
- MiniMax, test project and launcher Release builds: 0 warnings/errors.
- Corrected app reopened from `C:\Apps\minimax\bin\Release\net9.0-windows\MiniMax.exe`.
