# MiniMax → 9Router Token Refresh Bridge — Sequential Execution Plan

## Repository relocation

Current application/repository root: **`C:\Apps\minimax`**.
Moved from `C:\Apps\launcher\MiniMax` into the operator's existing `.git`
repository, without a nested `MiniMax\` directory. Historical `MiniMax\...`
file scopes below now mean paths relative to this repository root. App state
and the external 9Router database stay in their existing locations. Launcher
build/runtime references are updated separately under `C:\Apps\launcher`.

Move verification: all 149 original files matched their pre-move SHA-256 hashes.
Private `Backups\`, generated outputs and scratch are ignored by Git, not deleted.

Relocation verification completed: MiniMax, its test project and the launcher
built with 0 warnings/errors; 99 bridge tests and 9/9 check-in simulations passed
from the new root. Launcher runtime copies matched every file hash, and the
compiled launcher resolved `C:\Apps\minimax\bin\Release\net9.0-windows\MiniMax.exe`.
The old source folder is gone. No commit/push or OAuth token replacement occurred.

## Current amendment — automatic attach, email name, success closure

This amendment supersedes v1's manual Link state, profile-name connection labels,
and always-open Chrome behavior. The completed v1 ledger below remains historical
evidence; it is not the current product contract for those three behaviors.

The current single-click sequence is:

```text
Refresh on row
→ open and foreground related Chrome profile FIRST
→ native 9Router Global OAuth
→ detect visible enabled Authorize, then click
→ detect Authorization confirmed and await 9Router token completion
→ existing match: replace tokens, preserve UUID/settings, delete temporary row
  OR no match: keep the new connection and automatically remember its UUID
→ name connection with the row's Account email
→ persist automatic mapping
→ refresh quota using finalized UUID
→ gracefully close only this operation's owned Chrome window
→ Done
```

- No Link button/state or manual pairing dialog.
- Replacement preserves account count. First-time attachment adds exactly one
  connection; subsequent refreshes replace it without increasing the count.
- Unrecognized differently named old connections stay untouched.
- Missing/invalid email fails before starting Chrome/OAuth; no name is invented.
- No force kill. Failure/cancellation leaves Chrome open; closure is attempted
  only after connection/mapping/quotas succeed. A refused close is reported.
- ID-only recovery records and a source marker prevent duplicate OAuth after
  database commit but interrupted mapping persistence. A previous process's
  window is never blindly closed on recovery.

### Amendment execution evidence

| Step | Status | Evidence |
|---|---|---|
| Refactor UI/workflow/database/ownership | PASS | Refresh-only UI; replace-or-attach; email labels; owned-window graceful close; old Link dialog removed. |
| Focused fixture/regression checks | PASS | 99 bridge tests, 9/9 existing check-in simulations; empty-router attach, subsequent UUID reuse, interrupted persistence recovery, cancellation/failure and close ownership covered. |
| One existing-account live test | PASS | Back Up 10/Profile 31: name now `dhepil.backup10@gmail.com`; same UUID/priority/enabled/creation/proxy; both tokens changed; same 20-account ID set. One owned Chrome window closed; all four preexisting windows preserved. Backup `minimax-auto-attach-before-20261125-213625`. No unmatched account added live. |
| Final package/docs/cleanup | PASS | Workspace and test project builds: 0 warnings/errors. 99 bridge tests and 9/9 packaged check-in simulations passed. Runtime copy hashes all matched; no Link UI branch or credential patterns in source/docs. README/notes updated; job-owned scratch, fixtures and NuGet caches removed; backups preserved. |

---

## Original v1 plan and completed ledger (historical)

## Plan status

This plan is final and locked. It must be executed as a strict assembly line.

**Implementation completed 2026-11-25.** Every phase below passed in order.
Live verification covered Back Up 10/Profile 31 only; the other 19 accounts were
not reauthorized during testing. Unmatched rows require explicit one-time Link.

```text
Current phase → implement → verify → fix until PASS → record evidence → next phase
```

No later phase may start before the current phase passes its completion gate.
If a check fails, work remains in that phase until the failure is fixed.

## Progress ledger

Update this table during implementation. `PASS` requires the evidence demanded
by that phase; compilation alone is not enough.

| Phase | Status | Evidence |
|---|---|---|
| 0. Preserve baseline | PASS | 2026-11-25: 20 total/active Global accounts after operator removed discussion token; schemaVersion=1, backupSchemaVersion=2, appVersion=0.5.104. Verified online SQLite backup at `C:\Apps\9router\data\db\backups\minimax-bridge-baseline-20261125-202043` plus WAL/SHM and sanitized manifest; integrity_check=ok. Release build 0 warnings/errors; existing simulation 9/9. |
| 1. Paths and mapping contracts | PASS | Release build 0 warnings/errors; 11 mapping/path tests passed (round trips, duplicates, corruption, atomic replacement, URL confinement); existing simulation 9/9. |
| 2. Read and link existing accounts | PASS | Safe existing local CLI-auth account endpoint returned 20 Global accounts. 9 unambiguous normalized-name links; 11 router accounts explicitly unresolved (no matching email/name), never guessed. 19 focused tests passed; Release build clean; existing simulation 9/9. Discovery mapping written only to scratch. |
| 3. SQLite finalizer on fixture | PASS | Microsoft.Data.Sqlite 9.0.23 pinned; 33 focused tests passed, including fixture merge, metadata preservation, rollback, cancellation, count drift, wrong-provider/schema rejection and bounded busy timeout. Live DB only schema-read; Release build clean; existing simulation 9/9. |
| 4. Chrome profile activation | PASS | 38 focused tests passed; live Profile 31 blank window opened (handle 2951146) and foreground=True. No OAuth request. Release build clean; existing simulation 9/9. |
| 5. 9Router OAuth transport | PASS | 50 focused tests passed: validated device responses, Global-host restriction, missing fields, pending/slow-down/success, denied/expired/fatal, cancellation/deadlines. Unsupported user-code fallback explicitly rejected because installed route drops its flag. No live OAuth. Release build clean; existing simulation 9/9. |
| 6. Authorization-page automation | PASS | 67 focused tests passed: exact/hidden/disabled/ambiguous controls, Invoke priority and mouse fallback, delayed button, guarded retry, exact confirmation, cancellation/deadlines. OAuth reader does not reclaim focus from operator. Release build clean; existing check-in 9/9. |
| 7. Main-table integration | PASS | Four-column main table and explicit no-default pairing dialog tested without showing/running OAuth. Shared Stop token prevents overlap with check-in; focused single-job/cancellation guard test passed. 70 focused tests passed; Release build clean; existing check-in 9/9. |
| 8. Full dry end-to-end flow | PASS | 85 tests passed, including complete fake Chrome→native transport fake→Authorize→confirmation→fixture merge→quota chain, all stage failures, cancellation and recorded-source recovery. Fixed console harness synchronization-context deadlock before rerun. Release build clean; check-in 9/9. No live token writes. |
| 9. One-account production test | PASS | Back Up 10/Profile 31 actual GUI row button invoked once; profile opened before native OAuth; exact Authorize/Authorization confirmed gates completed, tokens merged and forced quota returned Done. Compared fresh backup: same UUID affd4653-13d9-45d8-b9e2-830b641eb56a, priority/enabled/creation/proxy preserved, access+refresh+expiry changed, exact name Back Up 10. Same 20-ID set, no temporary row. Backup: minimax-bridge-live-20261125-204633. |
| 10. Final regression and documentation | PASS | Whole workspace Release build 0 warnings/errors; 85 focused bridge tests; packaged MiniMax check-in 9/9; all MiniMax runtime files copied beside launcher with matching hashes; isolated source-only MiniMax build clean. Source/docs token-pattern scan clear. Final Global count=20 and SQLite integrity_check=ok. Updated README/notes; job-owned scratch/fixtures/NuGet caches removed, backups retained. No 9Router application source modified. |

## Execution protocol

For every phase:

1. Announce the active phase.
2. Mark only that phase `IN PROGRESS`.
3. Touch only the files listed in that phase's scope.
4. Implement the phase completely.
5. Build MiniMax.
6. Run the phase's focused checks.
7. Run every regression check required by the phase.
8. If anything fails, remain in the phase and fix it.
9. Record actual evidence in the progress ledger.
10. Mark the phase `PASS`.
11. Begin the next phase.

Hard rules:

- Never work on two phases at once.
- Never prepare later-phase code “while already here.”
- Never write the live 9Router database before fixture tests pass.
- Never run live OAuth before fake OAuth and fake page automation pass.
- Never test multiple real accounts before one real account passes.
- Never suppress, exclude or weaken a failing check to advance a phase.
- Never report unverified behavior as working.

Execution detail: focused tests live in `MiniMax\Tests\`, using a separate console
test project that compiles only the current phase's bridge sources. Test sources
are excluded from the production compile glob; this is a runtime boundary, not
an exclusion from verification. Its fixture/output/cache files use the job-owned
workspace scratch area. The test harness grows only as each phase starts.

---

# Locked product contract

## Target

The feature applies only to 9Router's **MiniMax Code (Global)** provider:

```text
minimax-code-global
```

It does not apply to Chinese `minimax-code` or the API-key provider named
`minimax`.

The installed 9Router database currently contains 20 active
`minimax-code-global` OAuth connections.

## Required user flow

```text
Click Refresh on one MiniMax row
        ↓
Open that row’s Chrome profile first
        ↓
Wait until that Chrome window is foreground/last-used
        ↓
Start 9Router’s existing MiniMax Global device OAuth flow
        ↓
Open the returned login URL normally
        ↓
Wait until the exact visible Authorize button is present
        ↓
Click Authorize
        ↓
Wait for the exact visible “Authorization confirmed” heading
        ↓
Wait for 9Router to receive and save the new tokens
        ↓
Merge the new tokens into the mapped existing connection
        ↓
Rename it exactly like the MiniMax profile
        ↓
Delete the temporary generic connection
        ↓
Force-refresh quota and credit information
```

The final global account count must remain 20. The existing connection UUID,
priority, enabled state, proxy settings, creation date and usage-history
relationship must remain unchanged.

## Ownership boundary

All bridge implementation lives under:

```text
C:\Apps\launcher\MiniMax\
```

Nothing under `C:\Apps\9router\app\` will be modified.

The bridge is:

- compiled into `MiniMax.exe`;
- made of internal classes;
- not a reusable library;
- not a separate executable or service;
- not an HTTP server;
- not exposed through a listening port;
- unavailable to other applications.

MiniMax may call 9Router's existing localhost endpoints and may perform the
final guarded SQLite transaction against 9Router's database.

## Planned final file structure

```text
MiniMax\
├── MiniMaxForm.cs
├── Models\
│   ├── ProfileRow.cs
│   └── NineRouterProfileLink.cs
└── Services\
    ├── NineRouterPaths.cs
    ├── NineRouterOAuthBridge.cs
    ├── NineRouterDatabaseBridge.cs
    ├── NineRouterProfileMapStore.cs
    ├── NineRouterChromeProfileStarter.cs
    ├── MiniMaxOAuthAuthorizationPage.cs
    └── MiniMaxOAuthAuthorizationRunner.cs
```

The main table's final shape is:

| Claimed | Profile | Account | Refresh token |
|---|---|---|---|
| ✓ | Back Up 10 | email@example.com | Refresh |

The Refresh token column contains an operation button. It never displays token
values.

Only one refresh operation may run at a time.

---

# Phase 0 — Preserve and prove the baseline

## Purpose

Create a trustworthy starting point before adding dependencies, touching Chrome
or writing any database.

## Scope

Read-only inspection and backups only. No feature implementation.

## Actions, in order

1. Read `C:\Users\YUZHA\.config\opencode\rules\chrome-profile.md` before any
   Chrome process/window interaction.
2. Read `C:\Users\YUZHA\.config\opencode\rules\workspace-temp.md` before
   creating fixtures or scratch files.
3. Record:
   - MiniMax target framework and project state;
   - installed 9Router version;
   - 9Router SQLite schema metadata;
   - current count of `minimax-code-global` connections;
   - current IDs, names, active states and priorities without recording tokens.
4. Copy the live database set to a dated backup location:

   ```text
   C:\Apps\9router\data\db\data.sqlite
   C:\Apps\9router\data\db\data.sqlite-wal
   C:\Apps\9router\data\db\data.sqlite-shm
   ```

5. Verify the backup is readable and contains the same provider count.
6. Build MiniMax in Release configuration.
7. Run the existing dry simulation.

## Required verification

- Release build: zero errors.
- Existing simulation: 9 passed, 0 failed.
- Backup opens read-only and reports the same schema/account count.
- Live database has not been modified.

## Completion gate

Do not start Phase 1 until all baseline checks pass and evidence is recorded.

---

# Phase 1 — Paths and mapping contracts

## Purpose

Create the stable identities and persisted mapping before networking, OAuth or
database writes exist.

## Files allowed in this phase

```text
MiniMax\Models\NineRouterProfileLink.cs
MiniMax\Services\NineRouterPaths.cs
MiniMax\Services\NineRouterProfileMapStore.cs
MiniMax\MiniMax.csproj                    (only if focused tests require it)
```

## Actions, in order

1. Define `NineRouterPaths` for:
   - local 9Router base URL;
   - database path;
   - WAL/SHM paths;
   - MiniMax mapping file path.
2. Define `NineRouterProfileLink` with:
   - Chrome `DirectoryName` as stable Chrome identity;
   - 9Router connection UUID as stable 9Router identity;
   - synchronized display name;
   - optional safe account email;
   - link timestamp/schema version.
3. Implement mapping load/save using an atomic temporary-write-and-replace
   pattern.
4. Reject duplicate Chrome directory keys and duplicate 9Router connection IDs.
5. Treat corrupt mapping data as an explicit recoverable error, not as an empty
   mapping that silently forgets ownership.

## Required verification

- Empty mapping round-trip.
- Multiple mapping round-trip.
- Duplicate Chrome profile rejected.
- Duplicate 9Router connection rejected.
- Corrupt file reported safely.
- Release build succeeds.
- Existing check-in simulation remains 9/9.

## Completion gate

Do not start Phase 2 until mapping persistence is deterministic and tested.

---

# Phase 2 — Read and link existing 9Router accounts

## Purpose

Resolve the existing 20 accounts without writing tokens or changing 9Router.

## Files allowed in this phase

```text
MiniMax\Services\NineRouterOAuthBridge.cs   (read-only account discovery only)
MiniMax\Services\NineRouterProfileMapStore.cs
MiniMax\Models\NineRouterProfileLink.cs
Focused tests/simulation files owned by this feature
```

## Actions, in order

1. Read the existing 9Router provider-client endpoint for
   `minimax-code-global` accounts.
2. Capture safe fields only:
   - connection UUID;
   - name;
   - active state;
   - priority;
   - safe identity returned by quota lookup when available.
3. Match existing MiniMax/Chrome rows in this order:
   - exact normalized account email;
   - exact normalized profile/connection name;
   - unresolved when zero or multiple candidates remain.
4. Never pair by list position or creation order.
5. Persist only unambiguous links.
6. Represent ambiguous/unmatched links explicitly for later one-time selection.

## Required verification

- Read operation never returns token values to the UI or logs.
- Exact email matching works.
- Normalized-name fallback works.
- Duplicate/ambiguous names remain unresolved.
- No 9Router row changes.
- Release build succeeds.
- Existing simulation remains 9/9.

## Completion gate

Do not start Phase 3 until every account is either safely linked or explicitly
marked unresolved. No guessing is permitted.

---

# Phase 3 — SQLite finalizer against a copied fixture only

## Purpose

Prove replacement and cleanup without touching the live 9Router database.

## Files allowed in this phase

```text
MiniMax\Services\NineRouterDatabaseBridge.cs
MiniMax\Services\NineRouterPaths.cs
MiniMax\MiniMax.csproj
Database fixture tests under MiniMax
```

## Actions, in order

1. Add `Microsoft.Data.Sqlite` (or the selected equivalent) to MiniMax.
2. Copy the backed-up database into the workspace's approved task scratch area.
3. Implement schema validation:
   - expected metadata/schema version;
   - expected `providerConnections` columns;
   - expected JSON data shape.
4. Implement one immediate SQLite transaction that:
   - loads temporary source connection;
   - loads existing target connection;
   - verifies both are `minimax-code-global` OAuth rows;
   - copies only the new OAuth token/expiry fields;
   - renames target to the MiniMax profile name;
   - marks target active/successful;
   - clears stale errors, backoff, rate limits and `modelLock_*` values;
   - preserves target UUID, priority, enabled state, proxy/provider settings,
     creation date and unrelated metadata;
   - deletes the temporary source row;
   - commits atomically.
5. Configure busy timeout and bounded retry for genuine SQLite locks.
6. Refuse unknown schemas rather than attempting a best-effort write.
7. Ensure no token value is logged.

## Required fixture tests

- Valid source/target merge.
- Target UUID preserved.
- Target name synchronized exactly.
- New tokens and expiry copied.
- Priority/proxy/enabled state preserved.
- Stale errors and locks cleared.
- Temporary source deleted.
- Account count unchanged.
- Wrong provider rejected.
- Wrong auth type rejected.
- Missing source rejected.
- Missing target rejected.
- Unknown schema rejected.
- Locked database handled without force.
- Forced mid-transaction failure rolls back every change.

## Regression verification

- Release build succeeds.
- Existing simulation remains 9/9.
- Live 9Router database remains byte-for-byte untouched by this phase.

## Completion gate

Do not start Phase 4 until every fixture test passes and rollback is proven.

---

# Phase 4 — Open and foreground the related Chrome profile

## Purpose

Prove the required ordering: correct Chrome profile first, OAuth later.

## Files allowed in this phase

```text
MiniMax\Services\NineRouterChromeProfileStarter.cs
MiniMax\Services\Win32Window.cs             (only reusable window mechanics)
Focused Chrome-window tests/helpers
```

## Actions, in order

1. Snapshot visible Chrome top-level windows.
2. Launch the selected row's real profile using Chrome's supported switch:

   ```text
   chrome.exe --profile-directory="<DirectoryName>" --new-window
   ```

3. Wait for the new visible Chrome window.
4. Focus the new window only when it is not already foreground.
5. Confirm the tracked handle is the foreground Chrome window.
6. Return that handle to the caller.
7. Support cancellation and bounded failure deadlines.

## Safety boundaries

- Do not write, copy, move, link or delete Chrome profile data.
- Do not use a remote-debugging port.
- Do not force-kill Chrome.
- Do not start OAuth in this phase.

## Required verification

- Selected profile opens in its own visible window.
- The returned handle belongs to the newly opened window.
- The window becomes foreground.
- Cancellation works during window wait.
- Existing Chrome windows are not mistaken for the new window.
- Release build succeeds.
- Existing simulation remains 9/9.

## Completion gate

Do not start Phase 5 until profile-first foreground behavior is observed and
recorded without any OAuth request.

---

# Phase 5 — 9Router OAuth transport state machine

## Purpose

Implement 9Router communication without browser clicking or live database
finalization.

## Files allowed in this phase

```text
MiniMax\Services\NineRouterOAuthBridge.cs
OAuth transport models owned by MiniMax
Fake/local HTTP tests owned by this feature
```

## Actions, in order

1. Implement device-code request:

   ```text
   GET /api/oauth/minimax-code-global/device-code
   ```

2. Parse and validate:
   - authorization URL;
   - user/device code;
   - PKCE verifier;
   - polling interval;
   - expiry.
3. Implement polling request:

   ```text
   POST /api/oauth/minimax-code-global/poll
   ```

4. Implement state handling:
   - `authorization_pending` → continue;
   - `slow_down` → increase interval;
   - `access_denied` → fail;
   - `expired_token` → fail;
   - fatal error → fail;
   - returned connection UUID → token delivery complete.
5. Honor cancellation during every wait/request.
6. Never log codes, verifiers or tokens beyond safe status metadata.

## Required verification using fake HTTP responses

- Valid device-code response.
- Missing authorization URL rejected.
- Missing verifier/code rejected.
- Pending loop.
- Slow-down interval adjustment.
- Denied flow.
- Expired flow.
- Fatal server error.
- Successful connection UUID.
- Cancellation.
- Timeout.

## Regression verification

- No real browser is opened.
- No live OAuth occurs.
- No database is written.
- Release build succeeds.
- Existing simulation remains 9/9.

## Completion gate

Do not start Phase 6 until the OAuth transport state machine passes every fake
response scenario.

---

# Phase 6 — Authorization-page automation

## Purpose

Prove exact visible-element gating before connecting it to live OAuth.

## Files allowed in this phase

```text
MiniMax\Services\MiniMaxOAuthAuthorizationPage.cs
MiniMax\Services\MiniMaxOAuthAuthorizationRunner.cs
MiniMax\Services\Win32Window.cs             (safe reusable actions only)
Fake OAuth-page simulation files
```

## Authorize-button contract

The element must be:

```text
ControlType.Button
accessible name exactly "Authorize"
class contains "bg-bg_interaction_primary_default"
enabled
visible and genuinely on-screen
valid non-zero coordinates
```

The nearby **Decline** button must never be accepted.

## Click contract

1. Read one page snapshot.
2. Confirm the exact button is present.
3. Use UI Automation `InvokePattern` when available.
4. Otherwise click the visible button center.
5. Retry at most once and only when the exact button remains visible and enabled
   after the retry deadline.
6. Never click on every poll.

## Confirmation contract

After clicking, wait for:

```text
accessible name exactly "Authorization confirmed"
class contains "mt-8 text-center"
visible and genuinely on-screen
```

The Authorize button disappearing is not success.

## Pointer/focus contract

- Focus Chrome only when not already foreground.
- Prefer InvokePattern so the real pointer does not move.
- Use real mouse movement only as fallback.
- Always release mouse-up in `finally`.
- Never refocus or reposition the pointer every polling cycle.
- Honor Stop and `Ctrl+Shift+S` cancellation.

## Required fake-page tests

- Delayed Authorize button.
- Decline present, Authorize absent.
- Hidden/off-screen Authorize ignored.
- Disabled Authorize ignored.
- Multiple ambiguous visible Authorize elements rejected.
- InvokePattern success.
- Mouse fallback success.
- Swallowed click retried once.
- Confirmation heading appears.
- Confirmation never appears.
- Cancellation before click.
- Cancellation after click.
- Timeout.

## Regression verification

- No live browser OAuth is run.
- Release build succeeds.
- Existing check-in simulation remains 9/9.

## Completion gate

Do not start Phase 7 until wrong/hidden controls are proven unclickable and the
confirmation heading is the only browser-side success condition.

---

# Phase 7 — Main-table integration

## Purpose

Assemble already-proven components into the MiniMax UI without running a real
authorization yet.

## Files allowed in this phase

```text
MiniMax\MiniMaxForm.cs
MiniMax\Models\ProfileRow.cs
MiniMax\Models\NineRouterProfileLink.cs
MiniMax\Services\MiniMaxOAuthRefreshRunner.cs (workflow wiring and single-job state)
MiniMax\Services\NineRouterPendingRefreshStore.cs (credential-free recovery journal)
MiniMax\UI\MiniMaxProfileLinkDialog.cs (one-time unresolved pairing)
Previously completed bridge services only when wiring reveals a contract defect
```

> **2026-11-26 correction:** this is now an active-attempt rollback journal, not
> a manual recovery blocker. Stop/failure removes the attempt's exact temporary
> UUID and clears the journal; stale state is discarded automatically on retry.
> See `MiniMaxFailedRefreshRollback.md`.

## Actions, in order

1. Add **Refresh token** button column to the current selected-profile table.
2. Never expose token strings in cells, tooltips, status or logs.
3. Add row operation states:

   ```text
   Refresh
   Opening profile
   Starting OAuth
   Waiting for Authorize
   Replacing token
   Done
   Failed
   ```

4. Bind each row to its stable profile mapping.
5. Show an explicit Link state for unresolved mappings rather than guessing.
6. Allow only one active refresh job globally.
7. Wire Stop and `Ctrl+Shift+S` to the active refresh cancellation token.
8. Leave check-in behavior and its existing buttons unchanged.

## Required verification

- Correct four-column table.
- No token values displayed.
- Linked row enables Refresh.
- Unlinked row cannot overwrite an arbitrary account.
- Second simultaneous refresh is blocked.
- Cancellation resets row/button state.
- Existing check-in flow still selects the same profiles.
- Layout persistence still works.
- Release build succeeds.
- Existing simulation remains 9/9.

## Completion gate

Do not start Phase 8 until the UI is wired but all external dependencies remain
fake/fixture-controlled.

---

# Phase 8 — Complete dry end-to-end execution

## Purpose

Run the whole operation as one workflow without touching a real account or live
database.

## Test flow

```text
row Refresh click
→ fake profile activation
→ fake device-code response
→ fake Authorize appears
→ click
→ fake Authorization confirmed
→ fake 9Router token completion
→ fixture temporary connection
→ fixture token merge
→ fixture duplicate deletion
→ fake forced quota refresh
→ row Done
```

## Required scenarios

- Full success.
- Chrome activation fails before OAuth starts.
- Device-code request fails.
- Authorize never appears.
- Authorization confirmation never appears.
- OAuth denied.
- OAuth expires.
- Database merge rejected.
- Database lock times out.
- Quota refresh fails after successful merge.
- Cancellation at each stage.
- Crash-recovery record after temporary connection creation.

## Required verification

- Target UUID preserved.
- Target name synchronized.
- Temporary row deleted.
- Fixture account count unchanged.
- Old target untouched on all pre-merge failures.
- Transaction rollback on merge failure.
- Quota failure reported separately after successful credential merge.
- Release build succeeds.
- Existing check-in simulation remains 9/9.
- All new dry-flow cases pass.

## Completion gate

Do not start Phase 9 until the complete fake workflow and every failure path
pass from one command.

---

# Phase 9 — One-account production test

## Purpose

Prove the feature against one real account only.

## Preconditions

- Phases 0–8 are marked PASS.
- Operator selects the one account to test.
- Live 9Router account count is recorded.
- Fresh database/WAL/SHM backup is created and verified.
- 9Router and MiniMax processes are in a known state.

## Actions, in order

1. Confirm the selected Chrome profile → existing 9Router UUID mapping.
2. Click Refresh once.
3. Verify the matching Chrome profile opens before OAuth starts.
4. Verify it becomes the foreground/last-used Chrome profile.
5. Verify the OAuth URL opens in that profile.
6. Verify MiniMax waits for the exact visible Authorize button.
7. Verify Authorize is clicked once.
8. Verify the exact visible `Authorization confirmed` heading appears.
9. Verify 9Router reports token completion.
10. Verify the temporary generic connection is merged and deleted.
11. Verify the existing target UUID is unchanged.
12. Verify its name exactly matches the MiniMax profile name.
13. Verify priority, active state and proxy settings are preserved.
14. Verify the global account count remains 20.
15. Verify forced quota/credit refresh succeeds for the preserved UUID.

## Failure rule

If any step fails:

- stop the production test;
- do not test another account;
- preserve logs without token values;
- inspect whether a recorded temporary connection needs the planned recovery;
- fix and repeat Phase 9 with one account only.

## Completion gate

Do not start Phase 10 and do not use the feature across the other accounts until
all one-account evidence passes.

---

# Phase 10 — Final regression, cleanup and documentation

## Purpose

Close the task honestly and leave no scratch or undocumented behavior.

## Actions, in order

1. Run Release build for the whole workspace.
2. Run existing check-in simulation: expect 9/9.
3. Run all new OAuth-page simulations.
4. Run all mapping tests.
5. Run all fixture-database tests.
6. Run the complete dry end-to-end workflow.
7. Sweep for:
   - secrets or token values in logs/source/docs;
   - accidental files outside `MiniMax\`;
   - stale temporary connections;
   - unclean scratch files;
   - unintended 9Router source modifications.
8. Remove task-owned `.agents-temp\` content.
9. Update:

   ```text
   MiniMax\9RouterMiniMaxOAuthNotes.md
   MiniMax\9RouterTokenRefreshBridgePlan.md
   README.md
   ```

10. Record exactly what was verified live and what remains unverified.

## Final completion gate

The feature is complete only when all phases show PASS and one Refresh click has
been proven to:

1. open the matching Chrome profile first;
2. open OAuth in that profile;
3. detect Authorize before clicking;
4. detect Authorization confirmed;
5. receive 9Router token completion;
6. update the existing connection UUID;
7. synchronize the name;
8. remove the generic duplicate;
9. preserve a total of 20 global accounts;
10. refresh quota/credit information for the preserved connection.
