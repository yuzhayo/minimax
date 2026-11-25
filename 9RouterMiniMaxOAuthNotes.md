# 9Router MiniMax OAuth Notes

Read-only investigation of the installed 9Router `0.5.104` under
`C:\Apps\9router`. This note concerns **MiniMax Code (Global)** only:
`minimax-code-global`, not the Chinese `minimax-code` provider or the API-key
provider named `minimax`.

## Implemented MiniMax bridge (2026-11-25)

The private bridge is implemented entirely in `C:\Apps\minimax\`; 9Router application
code was not modified. See `9RouterTokenRefreshBridgePlan.md` for gated execution
evidence.

The complete MiniMax project was moved from `C:\Apps\launcher\MiniMax` into its
own existing Git repository at `C:\Apps\minimax`. There is no nested MiniMax
project folder. Source, icons, tests, notes and private local backups were moved;
the repository's `.git` and `desktop.ini` were preserved. Persistent app state
and 9Router's database paths are unchanged. See `README.md` for current commands.

- Main-table **Refresh token** button opens/focuses the related Chrome profile
  first, then calls the native device-code endpoint and opens its URL normally.
- OAuth UI Automation waits for visible/enabled **Authorize**, prefers Invoke,
  and waits for visible **Authorization confirmed**. It does not reclaim focus
  from the operator or repeatedly move the cursor.
- Native 9Router polling returns the new generic connection ID. MiniMax performs
  an immediate guarded SQLite transaction to replace the mapped existing row's
  tokens/name and delete that new generic row. If no match exists, it keeps the
  new row instead and saves its UUID automatically. Both paths name the connection
  with the table's Account email.
- Existing UUID, priority, enabled state, creation date, proxy configuration and
  unrelated metadata are preserved. A force quota request uses the preserved ID.
- After persistence and quota refresh succeed, only the Chrome window opened by
  MiniMax closes (WM_CLOSE, no kill). Its process and a per-window ownership stamp
  are checked to prevent closing a preexisting or reused window handle.
- Installed schema guards: appVersion `0.5.104`, schemaVersion `1`,
  backupSchemaVersion `2`. Unknown versions are refused.
- `Microsoft.Data.Sqlite` `9.0.23` is the private database client; no listener,
  reusable service or custom 9Router endpoint exists.
- The existing 9Router CLI-auth header is derived locally from its machine ID
  and CLI secret. Neither secret nor OAuth credentials are written into notes,
  UI models or logs.
- The installed native poll route does not forward `_minimaxPollByUser` to its
  MiniMax handler. User-code-only fallback responses are explicitly rejected;
  normal device-code responses work (verified live).

### Pairing and recovery

- `%LOCALAPPDATA%\ChromeProfileLauncher\minimax-9router-links.json` stores stable
  Chrome folder → existing 9Router UUID links, with safe display metadata.
- Every row shows **Refresh**. There is no Link state or pairing dialog.
  An unmatched row attaches the newly authorized connection automatically; the
  next refresh reuses that saved UUID. An email-valued connection name also matches
  without email metadata. Differently named unrecognized connections are not
  guessed at or deleted.
- `%LOCALAPPDATA%\ChromeProfileLauncher\minimax-9router-pending.json` is a
  credential-free active-attempt journal. On Stop/failure, MiniMax uses its exact
  source UUID and creation boundary to remove only that attempt's temporary
  connection, then clears the journal. A stale journal is rolled back
  automatically before a retry; it never blocks all future attempts.
- No connection is guessed or deleted by name/order. A preexisting target stays
  unchanged when finalization does not commit. After process restart, an old
  window is not blindly closed because ownership cannot be re-established.

### Verification

- One real account tested: **Back Up 10 / Profile 31**.
- Preserved UUID: `affd4653-13d9-45d8-b9e2-830b641eb56a`.
- Updated live test: both tokens changed; connection name became
  `dhepil.backup10@gmail.com`.
- Same complete set of 20 Global UUIDs remained; no generic duplicate remained.
- Quota refresh completed successfully.
- The one opened Chrome window closed; all four preexisting Chrome windows remained.
- Release workspace build: 0 warnings, 0 errors. Bridge test suite: 99 passing.
  Existing check-in simulation: 9/9.
- Fresh pre-live backup:
  `C:\Apps\9router\data\db\backups\minimax-auto-attach-before-20261125-213625`.
- The other 19 accounts were not reauthorized as part of testing.

## Current local state

- 9Router stores its SQLite database at
  `C:\Apps\9router\data\db\data.sqlite`.
- The `providerConnections` table currently contains **20 active**
  `minimax-code-global` OAuth accounts.
- Each account is one independent row with `provider = minimax-code-global`
  and `authType = oauth`.
- Its JSON `data` field stores the access token, refresh token, expiry,
  test/error state, rate-limit state, last-use/refresh timestamps and model
  locks. Token values must never be copied into logs or documentation.

## What the Add button does

On the MiniMax Code (Global) provider page, **Add** selects the OAuth path and
opens the generic **Connect MiniMax Code (Global)** modal.

The modal automatically performs this sequence:

1. Calls the local 9Router endpoint:

   ```text
   GET /api/oauth/minimax-code-global/device-code
   ```

2. 9Router creates a PKCE verifier/challenge and sends a form request to:

   ```text
   POST https://account.minimax.io/oauth2/device/code
   ```

   with:

   ```text
   client_id=mcode-public
   scope=agent.default
   audience=agent-backend
   code_challenge_method=S256
   code_challenge=<generated PKCE challenge>
   ```

3. MiniMax returns a user code, device code, authorization URL, expiry and
   polling interval. The modal displays the login URL and code.

4. The dashboard calls `window.open(loginUrl, "_blank")`.

5. The Electron wrapper intercepts external URLs in
   `C:\Apps\9router\desktop\electron\main.ts` and calls
   `shell.openExternal(url)`. Windows therefore opens the default browser.
   Chrome opens only when Chrome is the Windows default browser.

6. While the modal says **Waiting for authorization**, it repeatedly calls:

   ```text
   POST /api/oauth/minimax-code-global/poll
   ```

   with the device/user code and PKCE verifier. The default interval is the
   value returned by MiniMax, normally about five seconds.

7. The local 9Router endpoint checks MiniMax through:

   ```text
   POST https://account.minimax.io/oauth2/token
   ```

   using the device-code grant, `client_id=mcode-public`, and the PKCE verifier.

8. Poll states are handled as follows:

   - `authorization_pending`: continue waiting.
   - `slow_down`: increase the poll interval.
   - `access_denied`: stop with failure.
   - `expired_token`: stop with failure.
   - access token returned: create the SQLite provider connection.

9. On success, 9Router saves the access token, refresh token and expiry in the
   `providerConnections` row, marks it active, closes the waiting state and
   refreshes the provider account list.

## Chrome boundary

9Router does **not**:

- choose a Chrome profile;
- launch Chrome with `--profile-directory`;
- automate or monitor the Chrome window;
- receive a callback from Chrome;
- close the browser tab after authorization.

It only asks Windows to open an external URL. The user authorizes in whichever
browser/profile Windows opens. Completion is detected by polling the MiniMax
token server, not by reading Chrome.

## Token refresh

MiniMax access tokens are refreshed at:

```text
POST https://account.minimax.io/oauth2/token
```

with:

```text
grant_type=refresh_token
refresh_token=<saved refresh token>
client_id=mcode-public
scope=agent.default
audience=agent-backend
```

The provider is configured to refresh before expiry. If MiniMax reports
`invalid_grant`, 9Router treats the sign-in as lost and requires authorization
again.

## Quota and credit display per account

For every account card, the dashboard calls:

```text
GET /api/usage/<provider-connection-id>
```

The server refreshes OAuth credentials if needed, then queries the global
MiniMax services using that account's access token:

1. Account identity:

   ```text
   https://agent.minimax.io/v1/api/user/info
   ```

2. Workspace/membership information through the signed MiniMax Agent API.
   This produces M Plan status/tier, expiry, group ID and remaining credits.

3. M Plan allowance windows:

   ```text
   https://platform.minimax.io/v1/api/openplatform/coding_plan/remains
   ```

The normalized quota result can contain:

- `Credits`: remaining credit balance, rendered as a money/credit balance;
- M Plan allowance windows such as model/general windows and their remaining
  percentages/reset times;
- plan label (`Free`, `M Plan`, or `M Plan <tier>`);
- plan expiry;
- account identity when available.

Each card is keyed by its unique provider connection ID, so every one of the 20
profiles receives and displays its own quota result.

The dashboard keeps a UI cache in browser local storage under
`quotaCacheData`. Automatic refresh, when enabled, runs once every 60 seconds
while the quota page is visible and pauses when the page is hidden. A manual
refresh requests `/api/usage/<id>?force=1`.

## Relevant installed files

The installed Next.js application is a production bundle, so most server code
is minified into these generated files:

- Provider definitions:
  `C:\Apps\9router\app\app\.next-cli-build\server\chunks\6100.js`
- MiniMax device OAuth implementation:
  `C:\Apps\9router\app\app\.next-cli-build\server\chunks\4049.js`
- Token refresh implementation:
  `C:\Apps\9router\app\app\.next-cli-build\server\chunks\3943.js`
- MiniMax quota/credit implementation:
  `C:\Apps\9router\app\app\.next-cli-build\server\chunks\7211.js`
- Generic OAuth route:
  `C:\Apps\9router\app\app\.next-cli-build\server\app\api\oauth\[provider]\[action]\route.js`
- Generic per-account usage route:
  `C:\Apps\9router\app\app\.next-cli-build\server\app\api\usage\[connectionId]\route.js`
- OAuth modal/dashboard bundle:
  `C:\Apps\9router\app\app\.next-cli-build\static\chunks\5497-d7896a9cac1a7182.js`
- Provider-page Add button bundle:
  `C:\Apps\9router\app\app\.next-cli-build\static\chunks\app\(dashboard)\dashboard\providers\[id]\page-ee82cb67a549308f.js`
- Electron external-browser handoff:
  `C:\Apps\9router\desktop\electron\main.ts`
