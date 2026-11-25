# MiniMax setting + refresh all — sequential plan

> **Current toolbar correction (2026-11-26):** `sign in all` sits between Check in
> and refresh all, and the standalone Stop was removed. The active operation's
> own button now becomes Stop/Stopping. Older toolbar descriptions below are
> historical phase records.

## Corrected midnight behavior (2026-11-26)

The previous midnight selection-clearing requirement/test below was wrong.
Current behavior: reset **Claimed only**; preserve all Run selections, the saved
selection file, displayed profiles and active operations. The original phase
ledger records the earlier implementation; this correction supersedes its
midnight cancellation wording. See `MiniMaxMidnightRecovery.md`.

Execute one phase at a time: implement → build → focused tests → existing
regressions → record PASS → next phase. Do not advance past a failing gate.

## Locked behavior

- Main toolbar: `setting | profile | Check in | refresh all | Stop | Clear claimed`.
- All buttons use `UI/MiniMaxToolbarButton.cs`.
- `setting` opens one modeless settings window, like the profile screen.
- Move the existing check-in URL label/input and validation/normalization there.
  Save valid URLs between launches; Check in reads that saved value.
- Profile selection, main-table columns and native OAuth/claim logic stay intact.
- `refresh all` uses only the rows displayed in the main table when clicked,
  in their displayed order, including rows marked Claimed.
- Every row awaits the exact same one-row refresh workflow (Chrome profile first,
  OAuth, Authorize/confirmation, token finalization, mapping, quota and closure).
- Report index/total and row status. No parallel refreshes or overlapping check-in.
- Stop/Ctrl+Shift+S cancels the active row and prevents the next row.
- Safe ordinary failures are reported per row. Failed/stopped attempts remove
  their exact temporary connection, clear their attempt journal, reset the row
  to Refresh, and never create a global retry blocker.
- No live refresh-all or live check-in is run as part of implementation testing.

## Ledger

| Phase | Status | Evidence |
|---|---|---|
| 0. Baseline | PASS | Release build 0 warnings/errors; 100 focused tests and existing check-in 9/9 passed. App closed gracefully; no live OAuth/check-in. |
| 1. Settings owner and screen | PASS | 112 focused tests and check-in 9/9 passed; Release build clean. Fixed malformed explicit-protocol fallback in URL validation before passing. Valid/invalid URLs, persistence/failure feedback, screen edits and backward-compatible window layouts verified. |
| 2. Move URL and add setting toolbar wiring | PASS | Release build clean; 113 tests and check-in 9/9 passed. Main URL field/input removed; saved setting used; repeated setting clicks reuse one visible owned window. Fixed Show(owner) reuse failure before passing. |
| 3. Sequential refresh-all and shared row operation | PASS | Release build clean; 122 tests and check-in 9/9 passed. Twenty-row ordering/no-overlap, Claimed inclusion, safe failure/recovery stop, Stop between/during rows, busy restoration/midnight cancellation, and a three-row full workflow chain through fixture database/Chrome close verified. No live refresh-all. |
| 4. UI/package/regression verification and cleanup | PASS | Actual message-pump UI check passed: six uniform buttons, no main URL, setting click/reuse/edit/save/validation with fixture settings. 122 tests and check-in 9/9 passed; MiniMax/test/launcher Release builds 0 warnings/errors; runtime hashes matched. README updated; source credential-pattern scan clear; job scratch/caches removed. No live refresh-all or check-in executed. |

The earlier Phase 3 phrase "safe failure/recovery stop" is historical and was
superseded on 2026-11-26. Failed state now rolls back and remains retryable; see
`MiniMaxFailedRefreshRollback.md`.

## Phase 0 — baseline

Inspect current source, persistent-state paths, row operation and tests. Preserve
unrelated work. Gracefully close the app for builds. Build and pass the current
100 focused tests and 9/9 browser-free check-in cases. Do not change features.

## Phase 1 — settings owner and screen

Files: `Services/MiniMaxSettingsStore.cs`, `MiniMaxSettingsForm.cs`,
`Services/WindowLayoutStore.cs` (new window bounds), focused settings tests.

Implement a default `https://agent.minimax.io/` setting, HTTP(S) validation,
existing URL normalization, atomic persistence, and visible validation/save
feedback. Invalid edits never replace the last saved valid URL. Create a minimal
dark modeless window with URL label/box, icon and saved window bounds.

Gate: URL normalization/validation, reload, corrupt-file handling, save failures,
screen update, backward-compatible layout round trip; build and regressions pass.

## Phase 2 — setting toolbar wiring and URL relocation

Files: `MiniMaxForm.cs`, focused UI tests. Wire the new screen as one owned
instance. Remove the main URL controls and `_txtUrl`; Check in uses the settings
store. Keep profile/check-in/Stop/Clear claimed behavior. Reserve the toolbar slot
for refresh all; the actual batch action is added only in Phase 3.

Gate: setting button opens/reuses its screen; URL is absent from main form and
present in settings; check-in reads saved URL; uniform buttons/labels fit; build
and regressions pass.

## Phase 3 — sequential refresh all

Files: `Services/MiniMaxOAuthRefreshBatchRunner.cs`, `MiniMaxForm.cs`, focused batch
tests. Extract an awaitable `RefreshRowAsync` operation used by both row buttons
and the batch; do not copy OAuth logic. Add refresh-all button. Hold the existing
busy/cancellation boundary across the complete chain and disable conflicting
actions/selection edits. Preserve per-row status/mapping updates. Midnight only
resets daily Claimed marks; it must not clear selection or cancel a batch.

Gate: displayed rows only, ordering, no overlap, no Claimed filtering, safe
failure handling, pending-recovery stop, cancellation between/during rows, busy
state restored, build and all regressions pass.

## Phase 4 — verification and completion

Verify both new buttons and modeless settings visually/through actual control
layout without running live account refreshes. Rebuild MiniMax and the launcher,
verify runtime copies, rerun full focused tests and 9/9 simulations. Update
README, record actual evidence, remove only this job's scratch/caches and reopen
the app. Preserve private backups and all existing account data.
