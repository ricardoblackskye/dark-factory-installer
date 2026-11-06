# ADR 0002: Prerequisite detection delegates to `df:sandbox-up --check`

- Status: Accepted
- Date: 2026-11-06
- Workstream: Dark Factory Installer — decision made in #4, recorded here for #16
- Supersedes: none
- See also: [ADR-0004](0004-documentation-update-strategy.md)

## Context

Before launching a sandbox, the installer must know whether the host meets the prerequisites
(.NET 8 SDK, Windows, and the CLI's required binaries). We could probe each tool in the GUI, but
that duplicates the single source of truth already encoded in agent-eve's headless CLI and its
`artifacts.lock.json` manifest (#603).

## Decision

Delegate prerequisite detection to `df:sandbox-up --check`.

- Branch on the **exit code**: `0` = ready, `10` = not ready. Never parse the stdout NDJSON
  event stream for status — it is the event stream, not the status.
- Read remediation from **stderr**, which carries the per-prerequisite `problems` (subject + fix)
  as prose; the GUI surfaces that text directly.
- Prefer `--check` over re-implementing probing; it keeps one source of truth with the CLI's
  refuse-early behaviour (implemented in #4).

## Consequences

- **Easy:** the GUI holds no prerequisite logic; a single `CliRunner.BuildCheckCommand()` call
  (`df:sandbox-up --check`) drives the whole pre-flight gate.
- **Hard:** the installer couples to the CLI's exit-code contract; a breaking change there must be
  reflected here, and the doc-drift guard catches a stale README.
- **Deferred:** richer structured problem reporting would require extending the CLI's stderr
  contract — out of scope for this ADR.
