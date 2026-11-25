# .NET 10 reusable application foundation — five-point plan

## Goal

Build one stable Windows application foundation that MiniMax consumes first.
A future application should reference the same foundation and add only its own:

- action definitions and operation logic;
- body elements, tables and columns;
- bottom status/progress messages;
- browser/page elements or other feature-specific services.

Proxy and Credit are deliberately deferred until the foundation is complete and
verified. They will be the first major proof that new features plug in without
editing the shell or existing siblings.

## Target shape

```text
Foundation/
  AppFoundation.csproj                 net10.0-windows reusable WinForms library

  Window/
    FoundationWindow.cs                Dumb wrapper: discover/place/connect only
    FoundationWindowAreaContract.cs    Area registration and relay contracts

  Areas/
    ActionBar/
      ActionBarArea.cs                 Discovers and places declared actions
      ActionBarActionContract.cs       Button/action/row-action declarations
      ActionBarOperationSession.cs     Start → Stop → Stopping coordination

    Body/
      BodyArea.cs                      Discovers and places body components
      BodyComponentContract.cs         Tables, columns and body-output contracts

    BottomText/
      BottomTextArea.cs                Owns every visible status/progress message
      BottomTextContract.cs            Named status/progress input channels

  Shared/
    UI/
      SharedActionButton.cs            Visual button primitive only
      SharedOutlinedTable.cs           Unified rows/selection/outline primitive
      SharedTextBlock.cs               Status/progress text primitive
      SharedTheme.cs                   Palette and generic control styling
      SharedAdaptiveWindow.cs          Count-to-window fitting mechanics
      SharedWindowLayout.cs            Reusable bounds/column persistence

MiniMax/
  MiniMax.csproj                       net10.0-windows app consuming Foundation
  Program.cs                           Supplies MiniMax assembly to the wrapper

  Areas/
    ActionBar/
      ActionBarCheckIn.cs
      ActionBarSignIn.cs
      ActionBarSignInAll.cs
      ActionBarRefresh.cs
      ActionBarRefreshAll.cs
      ActionBarSettings.cs
      ActionBarProfiles.cs
      ActionBarClearClaimed.cs

    Body/
      BodyProfilesTable.cs
      BodyClaimedColumn.cs
      BodyProfileColumn.cs
      BodyAccountColumn.cs
      BodySignInActionColumn.cs
      BodyRefreshActionColumn.cs

    BottomText/
      BottomTextStatus.cs
      BottomTextProgress.cs

  Features/
    CheckIn/
    SignIn/
    Refresh/
    Profiles/
    Settings/
    NineRouterBridge/

Tests/
  Foundation/
  MiniMax/
  SecondHostFixture/
```

## Connection model

```text
App-specific action child emits a typed request
                    ↓
ActionBarArea forwards through its declared output
                    ↓
FoundationWindow relays—without feature decisions
                    ↓
Feature service performs the operation
                    ↓
Typed result/progress output
          ├──────────────→ BodyArea updates body data
          └──────────────→ BottomTextArea displays all text
```

The window is a power strip: it provides sockets and connects compatible plugs.
It does not perform check-in, sign-in, refresh, proxy or credit calculations.

## Discovery rules

- `FoundationWindow` discovers `IFoundationWindowArea` declarations only from
  the application assembly explicitly supplied by `Program.cs`.
- `ActionBarArea`, `BodyArea` and `BottomTextArea` each discover only their own
  scoped child contract.
- Use normal .NET assembly reflection; add no discovery framework/package.
- Every declaration has a stable ID, placement/order and compatible contract.
- Duplicate IDs and incompatible contracts fail clearly; never select the first.
- Adding/removing a compatible child must not edit the window, area parent,
  central registration list or existing siblings.

## Shared boundary

`Shared/UI` contains visual mechanics even when only one current consumer uses
them. Reuse count does not decide ownership.

```text
SharedActionButton       Yes: generic appearance/input mechanics
SharedOutlinedTable      Yes: generic table presentation
ActionBarRefreshAll      No: MiniMax feature action
BodyCreditColumn         No: app/body feature child
BottomTextProgress       No: named BottomText-area child
```

Feature-specific children use Shared controls but stay under their owning area.
Shared never imports MiniMax feature implementations.

---

# Point 1 — migrate the foundation consumer to .NET 10

## Scope

- Change MiniMax from `net9.0-windows` to `net10.0-windows`.
- Change the MiniMax test project to `net10.0-windows`.
- Create `Foundation/AppFoundation.csproj` on `net10.0-windows` with WinForms.
- Keep WPF/UI Automation enabled only where browser automation requires it; do
  not make the generic Foundation depend on MiniMax browser behavior.
- Update the independent launcher's MiniMax copy path from
  `net9.0-windows` to `net10.0-windows`.
- Do not migrate the launcher application's own target framework without separate
  authorization; its build-order reference does not consume MiniMax assemblies.
- Verify the SQLite package/runtime combination under .NET 10 before changing
  package major versions. Do not combine an unnecessary data-library migration.

## Tasks

- [x] Record the net9 Release/test/runtime-hash baseline.
- [x] Add the Foundation project and build-order references.
- [x] Retarget MiniMax and MiniMax tests to net10.
- [x] Update launcher copy paths and packaging checks.
- [x] Remove or update .NET 9-specific warning comments/suppressions only when
      the .NET 10 build proves they are obsolete.
- [x] Pass all existing behavior/UI tests before structural movement begins.

## Gate

The .NET 10 binaries must behave identically before any area extraction. Runtime
migration and architecture migration are separate evidence points.

---

# Point 2 — replace MiniMaxForm with a dumb Window wrapper

## Scope

`FoundationWindow` may:

- discover the three declared areas;
- validate IDs/contracts;
- place Action Bar, Body and Bottom Text;
- relay typed requests/results;
- establish and clean up connections;
- persist generic window bounds through Shared mechanics.

It must not:

- construct individual MiniMax buttons or columns;
- start check-in/sign-in/refresh operations;
- own ProfileRow business state;
- calculate status strings;
- inspect area internals.

## Tasks

- [x] Define `IFoundationWindowArea` identity, placement and connection contracts.
- [x] Implement scoped reflection discovery from the supplied app assembly.
- [x] Implement duplicate/compatibility/order validation.
- [x] Add fixed area placement slots without feature imports.
- [x] Add narrow sibling relay contracts—never pass the entire Form/host.
- [x] Move generic lifecycle/bounds cleanup into the wrapper.
- [x] Reduce/remove `MiniMaxForm.cs` only after equivalent areas are connected.

## Gate

A test app must add/remove a compatible area declaration without editing
`FoundationWindow` or a central list.

---

# Point 3 — create Action Bar, Body and Bottom Text owners

## Action Bar

- Own every action definition and Start/Stop/Stopping mechanism.
- Discover toolbar actions and row-action declarations.
- Emit typed requests containing only required row/action data.
- Keep button visuals in Shared; keep MiniMax action decisions in MiniMax.

## Body

- Own the selected-profile table, rows, column placement and body interaction.
- Render row-action cells from Action Bar declarations relayed by the window.
- Emit row/selection requests without importing action implementations.
- Make future `BodyProxyColumn` and `BodyCreditColumn` additive children.

## Bottom Text

- Own every visible status and progress control.
- Receive named typed messages from feature outputs.
- Decide status/progress placement, not feature outcomes.
- Remove direct label writes and `SetStatus` calls from other areas.

## Tasks

- [x] Define each parent-local child contract and registration declaration.
- [x] Implement parent-local discovery, placement and cleanup.
- [x] Extract toolbar actions one child at a time.
- [x] Extract main table and current columns one child at a time.
- [x] Extract status and progress channels.
- [x] Route row Sign in/Refresh actions from Body to Action Bar through the
      window relay while keeping their buttons physically in the table.
- [x] Preserve profile/settings secondary windows as app-specific features; do
      not force them into the main three-area shell.

## Gate

No visible button is created by the window wrapper, no table element is created
by Action Bar, and no feature directly writes a Label.

---

# Point 4 — move pure UI mechanics into Shared

## Current-to-target mapping

```text
UI/DarkTheme.cs                  → Foundation/Shared/UI/SharedTheme.cs
UI/MiniMaxToolbarButton.cs       → Foundation/Shared/UI/SharedActionButton.cs
UI/MiniMaxTableHeightFitter.cs   → Foundation/Shared/UI/SharedAdaptiveWindow.cs
UI/WindowLayoutStore mechanics   → Foundation/Shared/UI/SharedWindowLayout.cs
UI/MiniMaxOperationSession.cs    → Foundation/Areas/ActionBar/
```

`MiniMaxOperationSession` moves to Action Bar because it coordinates application
actions; it is not a generic visual component.

## Tasks

- [x] Extract theme/palette without MiniMax feature imports.
- [x] Extract generic action-button states and visual variants.
- [x] Extract outlined/adaptive table presentation.
- [x] Extract generic status/progress text controls.
- [x] Extract reusable layout persistence contracts/storage mechanics.
- [x] Replace MiniMax UI implementations with Shared components.
- [x] Add dependency checks proving Shared does not reference MiniMax Areas,
      Features, Services or Models.

## Gate

The SecondHostFixture must render the same shell and visual primitives without
referencing any MiniMax assembly or type.

---

# Point 5 — move MiniMax behavior into discoverable feature children

## Scope

- Keep each feature's calculations/state/coordination together.
- Group the current flat Services by feature ownership without merging runtime
  boundaries such as browser UI Automation and 9Router SQLite access.
- Each area child declares its stable ID and narrow input/output contract.
- Existing features cannot import sibling implementations.

## Migration order

1. Settings/Profile navigation actions.
2. Clear Claimed and midnight Claimed reset.
3. Check-in action and outputs.
4. Sign-in row/batch actions and browser element flow.
5. Refresh row/batch actions and rollback journal.
6. Body table/columns and selected-row fitting.
7. Bottom status/progress messages.
8. Remove old adapters and the monolithic form only after parity.

## Tasks

- [x] Create MiniMax action/body/text child declarations with owner prefixes.
- [x] Move operation logic out of the wrapper without duplicating it.
- [x] Replace direct cross-feature calls with typed area/window contracts.
- [x] Preserve the 20-profile selection, Claimed-only midnight behavior,
      sign-in flow, refresh rollback and Chrome ownership rules.
- [x] Add a SecondHostFixture with one custom action, body component and bottom
      text channel using only Foundation contracts.
- [x] Prove a new fixture child requires no Foundation/parent/sibling edits.
- [x] Delete obsolete adapters/files only after all parity evidence passes.

## Gate

MiniMax and SecondHostFixture both run on the same Foundation. Their feature
assemblies remain separate, and Foundation imports neither app.

---

# Verification and completion

## Architecture checks

- [x] Window wrapper contains no MiniMax feature names or decisions.
- [x] Parents contain discovery/placement/relay/cleanup only.
- [x] Every child filename and declaration identify its owner.
- [x] No handwritten per-child parent registration list exists.
- [x] Duplicate IDs and incompatible contracts fail tests.
- [x] Shared imports no app-specific implementation.
- [x] Feature children import no siblings.

## Behavior parity

- [x] Current focused tests pass on net10.
- [x] Existing check-in simulation remains 9/9.
- [x] Actual UI shows all 20 selected profiles.
- [x] Stateful Stop works for all five operation kinds.
- [x] Midnight clears Claimed only.
- [x] Failed/stopped refresh remains immediately retryable.
- [x] Direct row Sign in closes its identified window; other flows retain their
      current window behavior.
- [x] No live sign-in/OAuth/check-in is required for migration verification.

## Packaging

- [x] MiniMax net10 Release build has 0 warnings/errors.
- [x] Launcher build finds and copies the net10 MiniMax output/dependencies.
- [x] Old net9 MiniMax output is not selected at runtime.
- [x] Task scratch/caches are removed and MiniMax reopens from the net10 path.

## Explicit boundaries

- Proxy and Credit behavior are not implemented in this foundation migration.
- No 9Router source changes.
- No Chrome profile relocation/copy or force-kill behavior.
- No rejected sign-in guardrail proposals.
- No unsolicited migration of the separate launcher application's own UI or
  target framework.

---

# Completion evidence — 2026-11-26

- MiniMax and its test runner migrated to `net10.0-windows`; Foundation and the
  second host also target .NET 10.
- The independent launcher remains .NET 9, builds MiniMax through an explicit
  cross-target MSBuild step and copies the net10 output. Copied/source MiniMax
  SHA-256 hashes matched; `AppFoundation.dll` is present beside the launcher.
- `MiniMaxForm.cs` is a thin `FoundationWindow` wrapper with no Build/Start/status
  workflow methods.
- Exactly three MiniMax areas are reflection-discovered and contract-validated.
- Six ActionBar children, five Body column children and two BottomText children
  are owner-prefixed and additive; no central child list exists.
- Pure visual mechanics live in `Foundation/Shared/UI`; Foundation references no
  MiniMax assembly.
- Feature-owned source is physically grouped under `Features/` and secondary
  windows remain with their owning profile/settings features.
- `SecondHostFixture` supplied its own action/body/text children and passed
  without a Foundation edit.
- 161 focused Foundation/architecture/behavior tests pass.
- Actual UI checks pass with all 20 profiles, stateful six-button toolbar,
  adaptive outlined table and settings screen; no live OAuth/check-in was run.
- Existing browser-free check-in simulation remains 9/9.
- Foundation, MiniMax, tests, second host and launcher builds completed with
  0 warnings and 0 errors.
