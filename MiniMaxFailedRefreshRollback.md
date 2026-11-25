# Failed/stopped refresh rollback — 2026-11-26

## Cause

The refresh runner intentionally preserved `minimax-9router-pending.json` after
an interrupted OAuth attempt. Every later row refresh rejected that file before
starting, and refresh-all treated it as a global recovery gate. One failed row
therefore blocked every retry.

## Correct behavior

- Each attempt records its exact temporary 9Router source UUID.
- Stop/failure removes only that exact UUID after validating provider, OAuth type,
  creation boundary, and the original target for replacement attempts.
- The target account is untouched unless the token merge transaction commits.
- The attempt journal clears after rollback.
- A stale journal is rolled back automatically at the beginning of a retry.
- A stopped/failed row returns to **Refresh**.
- Refresh-all counts an ordinary failed row and continues sequentially; no
  `RequiresRecovery`/`StoppedForRecovery` gate remains.

## Live stale-state cleanup

The stale attempt was verified before deletion:

- Profile: `Profile 82`
- Temporary source: `9f54babe-5584-4047-a082-7e1e5f258219` (`Account 19`)
- Existing target: `1ae66a68-4b94-41c8-812a-b65592cd4013`

The database and journal were backed up under
`Backups\FailedRefreshRollback-20261126-005617\`. Production rollback removed
only the temporary source. Exact before/after row comparison confirmed every
other provider connection was unchanged. The pending journal is gone, 9Router
has 18 MiniMax Global accounts and no generic `Account N`, and all 20 MiniMax
profile selections remain saved.

## Verification

- Exact-source rollback and unrelated/older-row rejection tests.
- Stop/failure rollback at OAuth, browser, merge, and cancellation stages.
- Immediate same-row retry after failed merge.
- Automatic stale-journal cleanup followed by a fresh retry.
- Failed new attachment cleanup and mapping-failure retry.
- Failed refresh-all row resets to Refresh and does not block the next row.
- Focused suite: 130 passing tests, 0 failed before final packaging checks.
