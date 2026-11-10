# Plan: #10 — WS8 Error handling & reporting

Status: DRAFT for review. Branch: `feat/10-error-handling` (off `origin/main` @ `734e705`, post #9 merge via PR #21).

## Gist
Complete the error-handling loop for the Dark Factory Installer: turn CLI exit codes and
`problems[].code` into clear, actionable, **classified** UI messages (operator-fixable vs internal),
and log secret-free diagnostics for bug reports. Builds on #4/#5 (refuse-early), #7 (exit codes),
and #8 (code→message mapping).

## Contract grounding (agent-eve #582 / #585 / #603)
- `df:sandbox-up --check` exits `0` ready / `10` not ready and writes per-tool `ERROR: <subject>` + `<fix>`
  to stderr (prose); stdout carries NDJSON events.
- A secret on argv (`--token`/`--password`/`--api-key`/`--secret`) is refused with exit `2`.
- The terminal `done` event carries `exitCode` and `problems[]`, each with a stable `code`
  (`ProvisionProblemCode`: `PrerequisitesMissing`, `PrerequisitesMismatch`, `ArtifactUnresolvable`,
  `ClusterUnreachable`, `ProofFailed`, `TeardownFailed`, `Internal`).
- Already in this repo:
  - `ProblemMessageMapper` (#8) — code → message for all 7 codes + `Internal`; covered by the
    `EveryCodeMapsToNonEmptyMessage` test.
  - `NdjsonEventParser.CodeMap` (#8) — hyphenated code → enum.
  - `PrerequisiteChecker` (#4) + `BinaryAcquisitionService` (#5) — refuse-early **before** provision;
    `MainWindow` surfaces the per-tool/per-binary `fix` text and blocks Provision.
- Gap: `ProvisionViewModel.RunProvisionAsync` ignores the returned `ProcessResult` (exit code), so a CLI
  crash *before* a `done` event leaves no error shown (AC1 hole); there is no operator-fixable vs
  internal distinction (AC3); and there is no structured, secret-free diagnostics block for bug reports
  (AC3/AC5).

## Design

### T1 — Domain: `ErrorCategory`
`enum ErrorCategory { OperatorFixable, Internal }` (Domain).

### T2 — Orchestration: `ExitCodeMapping`
`static class ExitCodeMapping` mapping `int` → `(string Message, string SuggestedAction, ErrorCategory)`:
- `0` → not an error (handled by the success path).
- `2` → **operator-fixable**: "The CLI refused a secret on the command line. Remove any
  `--token`/`--password`/`--api-key`/`--secret` and use a vault handle." (The installer never passes
  secrets itself, so this only fires on an operator override or a future change.)
- `10` → **operator-fixable**: "Prerequisites are not satisfied (exit 10). Run the prerequisite check
  and fix the listed tools, then retry."
- any other non-zero → **internal**: "An unexpected error occurred (exit `<code>`). This looks like an
  internal fault — please report it." (category Internal; diagnostics populated).

### T3 — Orchestration: `ErrorReport` + `ErrorClassifier`
- `record ErrorReport(ErrorCategory Category, string Headline, IReadOnlyList<string> Actions, string? Diagnostics)`.
- `static class ErrorClassifier`:
  - Input: `ProcessResult result`, optional `IReadOnlyList<ProvisionProblem>? problems` (from the `done`
    event), and optional originating `ProvisionStep`/phase for context.
  - If `problems` present: map each via `ProblemMessageMapper`; category = **OperatorFixable** for the
    known actionable codes (`Prerequisites*`, `ArtifactUnresolvable`, `ClusterUnreachable`, `ProofFailed`,
    `TeardownFailed`) and **Internal** for `Internal`. `Headline` = primary message; `Actions` = the
    messages.
  - Else if `result.ExitCode != 0`: use `ExitCodeMapping`.
  - Else: success (no report).
  - `Diagnostics` (internal only) = `exit=<code>; step=<last step>; phase=<phase>; ts=<utc>` — **never**
    includes argv or secrets (the installer passes only `--repo`/`--issue`/`--workspace-dir`).

### T4 — Orchestration: `ProvisionViewModel` wire-up
- `RunProvisionAsync` captures `var result = await _runner.RunAsync(...)` and tracks `_sawDone`.
- After the run: if not successful, build an `ErrorReport` via `ErrorClassifier`; set `Banner` (red) to
  the headline + suggested actions and expose a new `Diagnostics` property (populated only for internal).
  On success, clear `Diagnostics`.
- Route the existing `done`-event problems through the classifier so the operator-fixable vs internal
  split is consistent; surface `Diagnostics` only for internal failures.
- `ResetAndProvisionAsync` likewise captures the up-run `ProcessResult` and classifies if it fails.

### T5 — UI (`MainWindow`)
- **Operator-fixable**: red banner + the suggested actions (today's failure path, augmented).
- **Internal**: red banner + a read-only "Diagnostics (for bug report)" box (or Copy button) bound to
  `ProvisionViewModel.Diagnostics` — contains no secrets.
- AC4 confirmation: the prerequisite (`Check`) and binary (`Verify`) panels already render the
  per-tool/per-binary `fix` text and block Provision; add a one-line "See install instructions" pointer
  if not already present. Verify end-to-end that a missing prereq/binary surfaces *before* provision.

### T6 — Tests (TDD)
- `ExitCodeMappingTests`: `0` → not an error; `2`, `10` → operator-fixable with non-empty message;
  unknown → internal.
- `ErrorClassifierTests`: problems (e.g. `PrerequisitesMissing`) → operator-fixable; unknown exit + no
  problems → internal with diagnostics; success → no report.
- Extend `ProvisionViewModelTests`: process exits non-zero with no `done` event → red banner + mapped
  message; `done` failure with problems → operator-fixable headline; internal failure → `Diagnostics`
  populated (non-empty, no secret).

### T7 — README
- Document error handling: known exit codes, operator-fixable vs internal, and the secret-free
  diagnostics box. (Doc-drift G3 unaffected — no CLI command changes.)

## Acceptance-criteria coverage
- **AC1** — every non-zero exit code mapped (incl. crashes before `done`): T2/T3/T4. ✔
- **AC2** — `problems[].code` branches to correct remediation (extends #8): T3. ✔
- **AC3** — operator-fixable vs internal separated; internal carries bug-report context: T3/T4/T5. ✔
- **AC4** — refuse-early preserved end-to-end with install pointer: T5 (verify #4/#5 path). ✔
- **AC5** — errors logged with origin (event/exit), no secrets: T3 `Diagnostics` + existing `Log`. ✔

## Open questions / decisions for reviewer
- **Q1**: Treat exit `2` (secret-on-argv) as **operator-fixable** (recommended) — the installer never
  passes secrets, so this only fires on an operator override.
- **Q2**: Operator-fixable set = {`PrerequisitesMissing`, `PrerequisitesMismatch`, `ArtifactUnresolvable`,
  `ClusterUnreachable`, `ProofFailed`, `TeardownFailed`}; `Internal` → internal.
- **Q3**: `Diagnostics` includes exit code + last event + timestamp but never argv — confirm that is
  sufficient for a bug report and carries no secret risk.

## Branch / main
Branch `feat/10-error-handling` off `origin/main` @ `734e705` (post #9 merge, PR #21).
