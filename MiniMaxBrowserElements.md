# MiniMax browser element notes

Operator-provided elements used by the implemented row Sign in, sign in all and
refresh preflight workflows. Implementation was authorized on 2026-11-26.

## Required execution order

1. Element 1: sidebar `Sign in`.
2. Element 3: ensure the Terms of Service / Privacy Policy consent is ticked.
3. Element 2: click `Continue With Github` only after consent is confirmed ticked.
4. Continue submit step: find `Continue`; the `Continue as ...` username varies by account.
5. Return to the MiniMax page and confirm that Element 1 no longer displays `Sign in`.

The element numbers reflect the order they were supplied, not their execution order.

## Login success criterion

Operator-defined success: **Element 1 no longer shows the sidebar Sign in button**.
After the GitHub continuation returns to the loaded MiniMax page, the visible
button identified by `sidebar-signed-out-login` / accessible name `Sign in` must
be absent. It need not be physically removed from the DOM if it is no longer shown.

Do not mistake a GitHub page, a blank/loading page, an unavailable Chrome window,
or a failed/empty accessibility read for this success state. Evaluate absence
only after a valid read of the loaded MiniMax page. No extra success heading is
required for this login flow; OAuth's Authorization confirmed is a separate flow.

## 1. Sidebar Sign in button

- Role: button.
- Exact accessible name / aria-label: `Sign in`.
- Stable test ID: `sidebar-signed-out-login`.
- Indicates the signed-out sidebar's login entry point.
- Class identifiers include `h-8`, `w-full` and
  `bg-bg_interaction_primary_default`.
- The UI Automation implementation detects an enabled, visible on-screen button with
  accessible name `Sign in`. `data-testid` is a DOM identifier and is not assumed
  to be exposed by Windows UI Automation.
- This is not the OAuth `Authorize` button. Its disappearance from the loaded
  MiniMax sidebar is the login success condition defined above.

### Supplied HTML

```html
<button type="button" data-testid="sidebar-signed-out-login" aria-label="Sign in" class="flex h-8 cursor-pointer items-center justify-center overflow-hidden rounded-[8px] bg-bg_interaction_primary_default transition-colors hover:bg-bg_interaction_primary_hover w-full"><span class="animate-sidebar-expand-fade whitespace-nowrap text-sm leading-5 text-text_label_primary_default">Sign in</span></button>
```

## 3. Terms of Service / Privacy Policy consent toggle

- The supplied screenshot identifies the small check control beside
  `I have read and agree to the Terms of Service and Privacy Policy`.
- HTML role is a button styled as a 16-pixel consent checkbox/toggle.
- No explicit accessible name, `aria-checked`, `aria-pressed` or `data-testid`
  was supplied. Do not assume a UI Automation toggle state exists.
- Class identifiers include `mt-[2px]`, `size-4` and `shrink-0`.
- The supplied SVG and screenshot show a filled circle with a white check mark:
  the illustrated state is already ticked.
- Operator requirement: **this must be ticked before Element 2**.
- Implemented behavior: detect current state first. If already ticked, leave it alone.
  If unticked, click the parent button and verify the checked state before
  continuing. Never blindly toggle it, since that could remove existing consent.
- Use the nearby agreement text to distinguish this unlabeled control from other
  small icon buttons. Runtime state/selector coverage remains to be verified.

### Supplied HTML

```html
<button type="button" class="mt-[2px] flex size-4 shrink-0 items-center justify-center disabled:cursor-not-allowed"><svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><rect width="16" height="16" rx="8" fill="currentColor" class="text-text_default_primary"></rect><path d="M4.5 8L7 10.5L11.5 6" stroke="white" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"></path></svg></button>
```

## 4. Continue submit control (username varies)

- HTML element: `input`, with `type="submit"`.
- Visible value: `Continue`.
- Exact supplied accessible name / aria-label:
  `Continue as bluestaksdhepil-rgb`.
- The GitHub username is account-specific. Record `bluestaksdhepil-rgb` as this
  example only. **Operator correction: finding the Continue text is enough;
  do not require or hardcode a username.**
- Class identifiers: `btn`, `btn-sm`, `btn-primary`, `m-0`.
- No `data-testid` was supplied. In UI Automation, this submit input is
  expected to appear as a button with the aria-label as its accessible name;
  runtime exposure must be verified.
- Target the enabled, visible submit control with text/value `Continue` at this
  continuation step. Its aria-label may be `Continue as <any username>`; that
  suffix must not be used as an account-matching requirement.
- Keep detection scoped to the current continuation page/step so MiniMax's
  earlier email-form Continue button is not mistaken for this control.
- Follows the Continue With Github step. Success is checked after returning to
  MiniMax: Element 1 no longer shows Sign in.

### Supplied HTML

```html
<input class="btn btn-sm btn-primary m-0" aria-label="Continue as bluestaksdhepil-rgb" type="submit" value="Continue">
```

## 2. Continue With Github button

- Role: button.
- Exact visible label: `Continue With Github` (preserve spelling and capitalization).
- No explicit `aria-label` or `data-testid` was supplied; the text span provides
  the expected accessible name, which must be verified against UI Automation.
- Class identifiers include `h-[44px]`, `w-full`, `rounded-[10px]`,
  `border-border_default` and `bg-bg_default_primary`.
- Detection identifies the enabled, visible on-screen **parent button**
  named `Continue With Github`, not its nested SVG or text span.
- The SVG is the GitHub logo, a visual child, not a separate action.
- Requires the consent toggle (Element 3) to be confirmed ticked first.
- The next account-specific Continue control is recorded as Element 4. Login
  success is Element 1 no longer showing Sign in on MiniMax.

### Supplied full button HTML (including the separately highlighted SVG/span)

```html
<button type="button" class="flex h-[44px] w-full items-center justify-center gap-2 rounded-[10px] border border-border_default bg-bg_default_primary text-[16px] font-normal text-text_default_primary transition-colors hover:bg-bg_interaction_tertiary_hover disabled:cursor-not-allowed disabled:opacity-60"><svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 20 20" fill="none"><g clip-path="url(#clip0_496_12133)"><path d="M10 0C4.47788 0 0 4.59041 0 10.2531C0 14.7833 2.86531 18.6266 6.83866 19.9824C7.33841 20.0773 7.52193 19.76 7.52193 19.4891C7.52193 19.2447 7.5126 18.4369 7.50836 17.5802C4.72626 18.2004 4.13923 16.3704 4.13923 16.3704C3.68434 15.1853 3.02891 14.8703 3.02891 14.8703C2.12165 14.2339 3.0973 14.2469 3.0973 14.2469C4.1015 14.3193 4.63027 15.3035 4.63027 15.3035C5.52216 16.871 6.96964 16.4178 7.54028 16.1559C7.63001 15.4931 7.88921 15.0409 8.1752 14.7849C5.95406 14.5255 3.61909 13.6464 3.61909 9.7177C3.61909 8.5983 4.00974 7.6836 4.64948 6.96559C4.54564 6.7073 4.20338 5.6645 4.74634 4.25219C4.74634 4.25219 5.58608 3.97662 7.49707 5.3032C8.2947 5.07595 9.1502 4.96209 10 4.95823C10.8499 4.96209 11.706 5.07595 12.5052 5.3032C14.4139 3.97662 15.2525 4.25219 15.2525 4.25219C15.7968 5.6645 15.4544 6.7073 15.3505 6.96559C15.9917 7.6836 16.3797 8.5982 16.3797 9.7177C16.3797 13.6557 14.0403 14.5229 11.8135 14.7767C12.1722 15.0948 12.4918 15.7189 12.4918 16.6754C12.4918 18.0472 12.4802 19.1514 12.4802 19.4891C12.4802 19.762 12.6602 20.0817 13.1671 19.981C17.1383 18.6237 20 14.7818 20 10.2531C20 4.59041 15.5227 0 10 0Z" fill="black"></path></g><defs><clipPath id="clip0_496_12133"><rect width="20" height="20" fill="white"></rect></clipPath></defs></svg><span>Continue With Github</span></button>
```
