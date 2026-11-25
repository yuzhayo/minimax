# Rejected sign-in / refresh guardrail proposals — historical only

The operator explicitly did not approve these proposals. They are not active
requirements and must not be presented as blockers for the approved sign-in flow.

1. Signed in is not proof of the intended account: check the MiniMax account
   email against the row before replacing that row's credentials.
2. Match generic Continue only within the expected GitHub continuation step,
   with one visible enabled target; ignore its varying username suffix.
3. Unexpected password/2FA/passkey/CAPTCHA/unknown pages pause or fail the row
   for manual intervention, never blind continuation.
4. Reuse only the related profile's window; preserve preexisting tabs/windows
   and user-added work when cleaning up operation-owned browser resources.
5. Recheck the target window's foreground state immediately before OAuth opens
   its URL, because another profile may become last-used during sign-in.
6. All sign-in/check-in/refresh variants share one operation/cancellation lock.

Already discussed: a valid loaded MiniMax-page read is required before interpreting
Sign in absence; consent is checked before GitHub; operations run sequentially;
pre-finalization failures preserve existing credentials.

## Midnight correction (2026-11-26)

The operator clarified: midnight resets **Claimed only**, never Run/profile
selection. Do not clear selections or cancel batches solely because the day
changed. The previous selection-clearing behavior and tests were incorrect.

Read-only recovery investigation found checked-profiles.json empty, 18 current
MiniMax Global 9Router connections and 18 valid mappings. The earlier 20-account
backup additionally identifies yuzz 10/11, corresponding to Chrome Profile 78/79
(yuzz.cezh10@gmail.com and yuzz.cezh11@gmail.com). Restoring MiniMax's 20 selections
must not roll back or recreate 9Router token records as an unrelated operation.
