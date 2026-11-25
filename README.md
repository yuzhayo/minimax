# MiniMax

Independent Windows C# / .NET 10 WinForms application.
Repository root: `C:\Apps\minimax`.

## Build and run

```powershell
cd C:\Apps\minimax
& .\tools\Build-Dev.ps1
& .\bin\Release\net10.0-windows\MiniMax.exe
```

`version.props` is the single version source. Developer output is disposable;
normal daily use runs the installed GitHub release from `Portable\MiniMax.exe`.

## Source layout

```text
Features/                      Check-in, sign-in, refresh, profiles, settings, browser and 9Router behavior
Models/                        Profile and connection data
Tests/                         Focused behavior and UI verification
tools/                         Build, test and portable-release commands
Assets/                        App icon and original SVG artwork
Backups/                       Private local backups; ignored by Git
```

The **profile** button opens the full selection table. The main window shows only
selected accounts: **Claimed | Profile | Account | Sign in | Refresh token**.

The main toolbar is:

```text
setting | profile | Check in | sign in all | refresh all | Clear claimed
```

There is no standalone Stop button. Whichever operation button starts work becomes
**Stop**, then **Stopping…** after cancellation is requested, and restores its idle
label when cleanup finishes. This applies to Check in, sign in all, refresh all,
row Sign in and row Refresh. `Ctrl+Shift+S` uses the same Stop transition.

Both MiniMax tables use one dark row background, 32-pixel rows and one thin outline
around each whole row. Alternating stripes and individual cell boxes are removed;
selected-row highlighting remains. The main window counts its selected-profile
rows and adjusts its height within the current monitor so all of them are visible.

**setting** opens one reusable settings window. The check-in URL label and input
live there, not on the main toolbar. Valid HTTP(S) URL edits are normalized and
saved automatically in `%LOCALAPPDATA%\ChromeProfileLauncher\minimax-settings.json`.
Invalid edits leave the last saved valid URL unchanged. Check in reads that saved
value. The settings window's size/position is remembered with the other layouts.
It also displays the running version and a manual **Check for updates** action.
Portable builds silently check GitHub once per launch; the update popup appears
only when a newer stable release exists, then downloads, verifies, installs into
the current Portable folder and restarts MiniMax automatically.

**Sign in** opens the saved MiniMax URL for that row's Chrome profile. If the
profile window is already open, Chrome receives a new tab; otherwise one window
is created. A signed-out row follows the supplied flow: Sign in, checked consent,
Continue With Github, generic Continue, then return until Sign in disappears.
When Sign in is clicked directly on a row, its identified Chrome window closes
after success even when that window was reused; all tabs in that window close.
**sign in all** chains the sign-in flow for every displayed row without this
standalone-row completion close.

**Refresh** first opens the same profile session and runs that sign-in-state
preflight. It then opens 9Router's OAuth authorization as another tab in the same
profile window, waits for Authorize, clicks it, and waits for Authorization
confirmed. An existing connection is replaced without changing its UUID/settings;
an unmatched account is attached automatically. The 9Router name is the account
email. A successful refresh closes a window only when that refresh created it;
an existing reused profile window remains open. Its active row button or
Ctrl+Shift+S cancels; no Chrome process is force-killed.

**refresh all** awaits the same row refresh operation sequentially for the rows
currently displayed, in displayed order. Claimed marks do not exclude accounts.
Progress shows `index/total — email: status`. Stop cancels the active row and
prevents the next. Stop/failure rolls that attempt back: its exact temporary
9Router connection and attempt journal are removed, and the row returns to
**Refresh** so it can be retried. Ordinary failed rows in refresh-all are counted
and the chain continues; there is no global recovery blocker. Check-in, sign-in,
row refresh and their batches share the existing operation/Stop boundary, and
the profile/settings editors are frozen while a job runs. Midnight clears
**Claimed only**. Run/profile selections
stay saved and displayed, and a day rollover does not cancel active operations.

The private bridge targets 9Router `minimax-code-global` at
`http://127.0.0.1:20129`, with the database at
`C:\Apps\9router\data\db\data.sqlite`. No 9Router application source is modified.

## Persistent state

Selections, dated claims, layout, automatic UUID mappings and active-attempt state
remain under `%LOCALAPPDATA%\ChromeProfileLauncher`. Moving the repository does
not reset them. Token values never appear in the table, logs or documentation.

## Verification

```powershell
& .\bin\Release\net10.0-windows\MiniMax.exe --simulate
dotnet run --project Tests\MiniMaxBridgeTests.csproj -c Release -- .agents-temp\bridge-tests
```

The test runner uses local fixtures; its optional live modes must be requested
explicitly. Keep scratch/cache data under this repository's `.agents-temp\` and
remove task-owned leftovers afterward.

## Release

See [the portable release guide](docs/operations/release.md).

```powershell
& .\tools\Install-GitHubPortable.ps1
```

The Chrome Profile Launcher remains at `C:\Apps\launcher`. Its MiniMax button
prefers this repository's executable; its build-order reference does not share
any source with this app.

`bin/`, `obj/`, `.agents-temp/`, `Backups/` and `desktop.ini` are ignored by Git.
Private backups are retained locally, not committed.
