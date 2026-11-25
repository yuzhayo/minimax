# MiniMax stateful Stop and outlined rows — implementation plan

This plan updates the earlier approved UI discussion for the current application,
which now includes row `Sign in` and toolbar `sign in all`.

Implemented on 2026-11-26.

## Final idle layout

```text
Toolbar
setting | profile | Check in | sign in all | refresh all | Clear claimed

Main table
Claimed | Profile | Account | Sign in | Refresh token
```

The standalone toolbar `Stop` button is removed.

## Stateful Stop behavior

Think of every start button as also being that operation's brake pedal.

| Started from | While running | After Stop click | After operation ends |
|---|---|---|---|
| `Check in` | `Stop` | `Stopping…` | `Check in` |
| `sign in all` | `Stop` | `Stopping…` | `sign in all` |
| `refresh all` | `Stop` | `Stopping…` | `refresh all` |
| Row `Sign in` | `Stop` | `Stopping…` | `Sign in` |
| Row `Refresh` | `Stop` | `Stopping…` | `Refresh` |

Rules:

- Only the button that started the operation becomes Stop.
- The first Stop click cancels the shared operation token and changes the active
  button to `Stopping…`; repeated clicks do nothing.
- Other toolbar operation buttons are disabled while one operation runs.
- For a one-row operation, only that row's active action cell remains clickable;
  clicks on every other row/action are ignored until completion.
- For a toolbar batch, the main table is disabled; its toolbar button remains
  clickable as Stop.
- `setting`, `profile`, and `Clear claimed` remain disabled while busy.
- `Ctrl+Shift+S` calls the same Stop request as the active button.
- Closing MiniMax during work calls the same Stop request, waits for existing
  cleanup, and then closes as it does now.
- Completion, cancellation, and failure all restore canonical idle labels.
- Existing refresh rollback remains responsible for removing a stopped attempt's
  temporary 9Router connection and journal. The UI does not duplicate rollback.

## Row appearance

- Remove alternating stripe colors.
- Use the same dark background for every unselected row.
- Set row height to 32 pixels as the initial value.
- Remove individual cell borders.
- Draw one thin outline around the complete visible width of each row.
- Preserve the existing selected-row background and white selected text.
- Preserve all current columns, checkboxes, action buttons, widths and layout
  persistence.
- Apply the same row treatment to the main table and profile-selection table.
- Do not change the separate Chrome Profile Launcher UI.

## Ownership and files

```text
MiniMaxForm.cs
  Parent/composition owner
  - identifies which toolbar or row action started
  - relays its cancellation token to the existing service
  - routes button/hotkey/window-close Stop requests
  - applies and restores button labels/enabled state

UI/MiniMaxOperationSession.cs
  Small reusable operation-state block
  - operation kind and optional active row
  - Running/Stopping state
  - one CancellationTokenSource
  - idempotent RequestStop and cleanup

UI/DarkTheme.cs
  Grid presentation owner
  - one row background
  - 32-pixel row template
  - no per-cell borders
  - whole-row outline painting

Tests/MiniMaxOperationUiTests.cs
  Focused state, cancellation, label, click-routing and row-rendering tests
```

The check-in, sign-in, OAuth, refresh-all, database and Chrome services keep their
current responsibilities. They already accept cancellation tokens and require no
new Stop workflow.

## Implementation tasks

- [x] 1. Capture the current passing baseline and actual seven-button UI layout.
- [x] 2. Add `MiniMaxOperationSession` with five operation kinds:
      CheckIn, SignInAll, RefreshAll, SignInRow and RefreshRow.
- [x] 3. Replace `MiniMaxForm._cancel` and scattered busy-state decisions with
      one active operation session.
- [x] 4. Remove `_btnStop` and its toolbar column; keep the current button order.
- [x] 5. Route toolbar active-button clicks through Start → Stop → Stopping….
- [x] 6. Route active row Sign in/Refresh clicks through the same mechanism while
      ignoring unrelated row action clicks.
- [x] 7. Route Ctrl+Shift+S and FormClosing through the same idempotent Stop path.
- [x] 8. Restore every canonical label and enabled state in one EndOperation path.
- [x] 9. Update `DarkTheme.StyleGrid`: no stripes, 32-pixel rows, no cell borders,
      and one whole-row outline painter.
- [x] 10. Apply/verify styling in both MiniMax grids without changing layout data.
- [x] 11. Update the toolbar, README and historical UI notes to the implemented
      six-button toolbar (including `sign in all`, excluding standalone Stop).
- [x] 12. Run focused tests, 9/9 check-in simulation, actual 20-row UI verification,
      MiniMax/test/launcher Release builds, cleanup and app restart.

## Completion evidence

- 149 focused tests pass, including all five operation kinds, idempotent Stop,
  toolbar/row/hotkey routing, midnight preservation and both-grid row styling.
- Actual message-pump UI: 20 outlined rows, cezh10/11 present, six-button stateful
  toolbar, no standalone Stop, no OAuth/check-in triggered.
- Existing check-in simulation: 9/9.
- MiniMax, test and launcher Release builds: 0 warnings/errors.

## Verification matrix

### Operation state

- Every one of the five start sources displays Stop only while it owns the job.
- Stop changes exactly once to Stopping… and cancellation is requested exactly once.
- A stopped batch never starts the next row.
- A stopped row refresh performs the already-implemented rollback and is retryable.
- Failure and cancellation cannot leave a permanent Stop/Stopping label.
- Midnight still clears Claimed only and does not cancel an operation.

### Interaction

- The active row Sign in/Refresh cell remains mouse-clickable as Stop.
- Other row action clicks and unrelated toolbar actions do nothing while busy.
- Ctrl+Shift+S and window close use the same Stop state transition.
- No overlapping check-in, sign-in or refresh operation can start.

### Appearance

- Main and profile grids have no alternating stripe color.
- Row template and displayed rows are 32 pixels high.
- No individual cell boxes appear.
- Exactly one thin outline encloses each whole row.
- Selected-row highlighting remains visible over the unified background.

### Regression/package

- All existing sign-in preflight, refresh rollback, 20-profile persistence and
  midnight tests continue to pass.
- No live sign-in, OAuth, refresh-all or check-in is required for verification.
- MiniMax and launcher Release builds complete without warnings/errors.

## Explicit boundaries

- No changes to sign-in element matching or the rejected guardrail proposals.
- No changes to OAuth/token merge rules or 9Router source.
- No Chrome profile data, sign-in state, user-data directory or process is
  force-modified.
- No changes to the separate launcher UI.
