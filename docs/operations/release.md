# MiniMax portable release

MiniMax ships as a self-contained `win-x64` portable ZIP. The destination PC
does not need a separate .NET 10 Desktop Runtime installation.

## Version source

`version.props` is the only checked-in version source. It supplies package,
assembly, file and informational versions to every project through
`Directory.Build.props`. A development bump advances the checked version only
after its exact build passes. Release resolution compares that checked version
with immutable `v*` tags, so production reuses a prepared version instead of
bumping it twice.

## One-click development and production

The two user-facing launchers live under `tools/`. Their BAT files are thin
Windows entrypoints; PowerShell owns validation and workflow policy.

Development prepares the next patch, runs the clean build/test path, saves the
version after success and launches the developer executable:

```bat
tools\bump-build-run.bat
```

Production can be run after development or directly. It reuses an already
prepared version, otherwise prepares the requested bump, then requires explicit
confirmation before staging all listed changes. It commits, pushes `main`, waits
for exact-commit CI, dispatches the release workflow and independently verifies
the published ZIP, checksum, manifest, executable version and simulation:

```bat
tools\bump-build-commit-push-release.bat
tools\bump-build-commit-push-release.bat minor "Describe the release"
tools\bump-build-commit-push-release.bat major "Describe the release"
```

The default bump is `patch`. If the commit message is omitted, production asks
for one. Planning/test mode exists only on the PowerShell owners and performs no
version, Git or publication changes.

## Local package

```powershell
.\tools\Test.ps1
.\tools\Build-Portable.ps1
```

Outputs:

```text
artifacts/releases/MiniMax-v<version>-win-x64-portable.zip
artifacts/releases/MiniMax-v<version>-win-x64-portable.zip.sha256
```

The package script validates Windows file/product versions and required runtime
dependencies, and runs the published executable's browser-free simulation before
creating the ZIP.
Packaging staging and RID/helper build output are removed after success.

Do not distribute `MiniMax.exe` alone. The app requires the other files in the
portable ZIP.

## GitHub release

`.github/workflows/ci.yml` uses the same local test/package scripts, including
the two-launcher planning contract check, on every pull
request and every `main` commit. `.github/workflows/release.yml` is manually
dispatched with a patch, minor or major bump. It releases only the exact `main`
commit whose `build` check is green.

The first release is `v1.0.0`. Later releases derive from the highest `v*` tag.
The workflow publishes the portable ZIP and its SHA-256 file to GitHub Releases.

## Daily Portable installation and updates

`tools/Install-GitHubPortable.ps1` downloads and verifies a GitHub release before
replacing the ignored `Portable/` folder. The launcher prefers that released copy
over developer output.

Portable MiniMax checks the latest stable GitHub release once per launch in the
background. It stays silent when current/offline; only a newer version opens the
automatic download/install/restart popup. Development builds display their version
but never overwrite `bin/` from GitHub.

## Deliberate limits

- Portable ZIP only; no installer or delta package. Updates replace the verified
  complete portable payload.
- Unsigned executable; Windows SmartScreen may warn.
- User state remains in the existing ChromeProfileLauncher LocalAppData location.
