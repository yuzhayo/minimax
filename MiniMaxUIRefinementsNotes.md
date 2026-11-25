# Implemented MiniMax UI refinements

Implemented on 2026-11-26 for the current toolbar, including `sign in all` and
the row Sign in action. See `MiniMaxStatefulStopAndRowsPlan.md`.

## Implemented: start/stop states on the active button

- Remove the standalone toolbar Stop button.
- Idle toolbar: `setting | profile | Check in | sign in all | refresh all | Clear claimed`.
- Check in, sign in all and refresh all become Stop while their jobs run.
- Row Sign in and Refresh become Stop for their one-row operations.
- Clicking the active button cancels that operation and changes it to Stopping….
- Restore the original label after success, cancellation or failure.
- Keep the other start button disabled to prevent overlap.
- Retain Ctrl+Shift+S.
- Preserve mouse cancellation for one-row refresh: the active row must remain
  clickable to stop its own refresh; other rows stay blocked.
- Existing check-in/OAuth/database workflows stay unchanged.

Owner: `MiniMaxForm.cs`, with reusable presentation mechanics in
`UI/MiniMaxToolbarButton.cs` if needed.

## Implemented: clearer table rows

- Remove alternating stripe colors. Use one dark background for every unselected row.
- Add a little row height; proposed starting point: 32 pixels.
- Draw one clear, thin outline around each whole row—not individual boxes around
  every cell, and not a GroupBox outline around the table.
- Keep selected-row highlighting and existing columns, checkboxes and buttons.
- Apply through the MiniMax grid style so the main and profile-selection tables
  remain consistent; the separate launcher is untouched.

Owner: `UI/DarkTheme.cs` grid styling. Row height and outline color are presentation
settings, separate from feature calculations.

Implemented with focused operation-state and whole-row rendering tests.
