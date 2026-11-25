# MiniMax project instructions

## Current product

- Windows .NET 10 WinForms application, versioned from `version.props`.
- Main toolbar: setting, profile, Check in, sign in all, refresh all, Clear claimed.
- Main table: Claimed, Profile, Account, Sign in, Refresh token.
- Proxy and Credit are not implemented.

## Current ownership

- `MiniMaxForm.cs` is composition-only.
- `Areas/ActionBar/` owns actions and Start/Stop/Stopping coordination.
- `Areas/Body/` owns the main table and column placement.
- `Areas/BottomText/` owns main-window status/progress text.
- `Features/` owns check-in, sign-in, refresh, update, profile, settings, browser and 9Router behavior.
- `Foundation/` and `SecondHostFixture/` are internal engineering details. Do not advertise them in public README/release text.
- Reusable visual mechanics stay internal under `Foundation/Shared/UI/`; feature controllers stay with their owner.

## Required behavior

- Midnight clears Claimed only; selections and active work remain.
- Failed/stopped refresh removes its exact temporary connection and stays retryable.
- Direct row Sign in closes its identified Chrome window; sign in all and refresh retain their existing window rules.
- Stop is stateful on the active toolbar/row control; Ctrl+Shift+S uses the same cancellation path.
- Chrome uses its real default user-data directory and is never force-killed.
- No 9Router application source changes.

## State and release

- Persistent app state remains under `%LOCALAPPDATA%\ChromeProfileLauncher`.
- GitHub Releases is the permanent release archive.
- `Portable/` is the ignored daily-release installation; `bin/Release/net10.0-windows/` is disposable developer output.
- Portable checks GitHub once per launch and shows an update popup only for a newer stable release.
- Development builds display the version but never install GitHub releases.
- `tools/Build-Dev.ps1` replaces generated developer output and removes helper/intermediate junk.
- `tools/Install-GitHubPortable.ps1` verifies and atomically installs a published release into `Portable/`.
- `tools/Test.ps1` is the shared CI test path.
- `tools/Build-Portable.ps1` creates the self-contained win-x64 release candidate.
- Public documentation describes MiniMax only, not its internal Foundation.

## Verification

- Run the focused test executable, second-host fixture verification and 9/9 browser-free simulation.
- Preserve 20 selected profiles, 18 MiniMax Global accounts and an empty pending-refresh journal.
- Report actual build/test/release results; do not equate a local package with a published GitHub release.
