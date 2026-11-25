# MiniMax sign-in buttons and refresh preflight — task list

Approved scope: implement the operator-provided sign-in flow. The previously
suggested guardrails are not part of this work.

## Design

```text
MiniMaxForm.cs
  UI/composition: row Sign in, toolbar sign in all, shared Stop/busy boundary

Models/ProfileRow.cs
  Per-row Sign in operation label

Services/NineRouterChromeProfileStarter.cs
  Open the requested profile URL in an existing profile window as a new tab,
  or create one window when that profile has no open window

Services/MiniMaxSignInPage.cs
  Read and press the already-recorded Sign in, consent, Continue With Github,
  and generic Continue controls

Services/MiniMaxSignInRunner.cs
  Execute one row's sign-in sequence and expose the same preflight to refresh

Services/MiniMaxSignInBatchRunner.cs
  Chain displayed rows sequentially for sign in all

Services/MiniMaxOAuthRefreshRunner.cs
  Open/check sign-in first, then open OAuth in a new tab in that same profile
  session; refresh-all remains a caller of the shared row refresh
```

## Tasks

- [x] 1. Change the Chrome profile session from always-new-window to
      reuse-window/new-tab-or-create-window behavior.
- [x] 2. Implement the recorded MiniMax/GitHub page elements.
- [x] 3. Implement one-row sign-in and sign-in-all sequencing.
- [x] 4. Add row `Sign in` and toolbar `sign in all` controls.
- [x] 5. Add sign-in preflight to the existing row refresh operation.
- [x] 6. Prove refresh-all inherits the preflight without separate OAuth logic.
- [x] 7. Run focused tests, check-in simulation, Release builds, UI verification,
      launcher build, cleanup, and reopen MiniMax.

## Non-goals

- No extra account-email verification step.
- No password, 2FA, passkey, CAPTCHA, or unrelated page automation.
- No 9Router source changes.
- No Chrome profile relocation/copy and no force-killing Chrome.

## Completion evidence

- Browser session fixtures prove existing-window reuse, new-window creation,
  same-window OAuth tabs, ownership-aware close and cancellation.
- Recorded element and flow fixtures prove Sign in → consent → Continue With
  Github → generic Continue → loaded MiniMax without Sign in.
- Refresh workflow fixtures prove sign-in preflight completes before OAuth and
  refresh-all still calls only the shared row refresh operation.
- The later stateful-Stop implementation supersedes the original seven-button
  toolbar: 20 restored rows, Sign in/Refresh columns and six idle toolbar buttons
  including sign in all, with no standalone Stop.
- 141 focused tests and existing check-in simulation 9/9 pass.
- MiniMax, test and launcher Release builds complete with 0 warnings/errors.

## Standalone row completion addition

Approved afterward: only a direct row `Sign in` closes its identified Chrome
window after the sign-in flow, including a reused window and all its tabs.
`sign in all` is unchanged, and refresh preflight keeps reused windows for OAuth.
