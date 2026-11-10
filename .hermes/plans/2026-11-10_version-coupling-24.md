# Plan: #24 — Declare + enforce a minimum agent-eve version (version coupling)

## Context
Epic #584 requires the Windows program to "declare a minimum Eve version and refuse a checkout it
does not support, so 'which sandbox did this run use' stays answerable." The plumbing already exists
but is **not active on the path the UI actually uses**:

- `CliVersionGate` (implements `IVersionGate`) runs `df:sandbox-up --version`, parses the semver, and
  compares it to `SandboxRunRequest.MinimumCliVersion`.
- `SandboxOrchestrator` (issue #7) calls the gate at `OrchestrationStage.VersionGate` — **but the UI
  does not use `SandboxOrchestrator`**. `MainWindow` drives `ProvisionViewModel` directly, and
  `ProvisionViewModel` **never calls any version gate**.
- `SandboxRunRequest.MinimumCliVersion` defaults to `null`, so even when the gate is invoked it always
  short-circuits to `Ok` — no minimum is ever declared.
- agent-eve #582 T1 added the `df:sandbox-up --version` flag (prints `package.json` semver to stdout,
  exit 0). agent-eve's package version is currently `0.0.0` (dev — no released semver).

## Goal / AC (from #24)
1. Installer reads the agent-eve CLI version (via `df:sandbox-up --version`) **before provisioning**.
2. If the checked-out agent-eve is below the declared minimum, the program **refuses** with a clear,
   operator-fixable message and performs **no mutation**.
3. Covered by a unit test on the version-guard path.

## Approach
1. **Single source of truth for the floor.** Add `InstallerContract.MinimumCliVersion` (a `const`
   string, SemVer constraint) in `DarkFactoryInstaller.Domain` — one place to bump per agent-eve
   release. Default proposed: `">=0.0.0"` (structural floor; see Q1).
2. **Fix `CliVersionGate`.** Change its constructor from `(IProcessRunner, SandboxRunRequest)` to
   `(IProcessRunner, string minimumVersion)` so it can be built once with the declared floor (no
   throwaway request). Replace the bare `df:sandbox-up` / `--version` TODO invocation with
   `npm run df:sandbox-up -- --version` (matching `CliCommands.Up`), so it resolves the npm script and
   runs in the same cwd as the provision command. Keep the semver parse + `VersionConstraint`
   comparison and the `Ok`/`Unsupported` short-circuit on an empty minimum.
3. **Wire the gate into `ProvisionViewModel`.** Inject `IVersionGate` (3rd ctor arg, alongside
   `_runner`/`_store`). In **both** `RunProvisionAsync` and `ResetAndProvisionAsync`, call
   `await _versionGate.CheckAsync(ct)` **before** invoking `CliCommands.Up`. On `!Supported`, set an
   operator-fixable failure banner (red, no mutation, up never starts) and return.
4. **Wire `MainWindow`.** Construct `ProvisionViewModel` with
   `new CliVersionGate(new SystemProcessRunner(), InstallerContract.MinimumCliVersion)`.
5. **Update `SandboxOrchestrator`.** Change its `CliVersionGate` construction to the new signature
   (`request.MinimumCliVersion ?? ""`) — no behaviour change (it already gates).

## Tests (TDD)
- **`CliVersionGateTests`** (new): fake `IProcessRunner` returns `1.2.3` → `Ok` against `>=1.0.0`;
  `Unsupported` against `>=2.0.0`; process error / non-zero exit → `Unsupported`; assert the invoked
  command is `npm run df:sandbox-up -- --version`.
- **`ProvisionViewModelTests`**: extend the existing fake `IStreamingProcessRunner` to record whether
  `RunAsync` was called. New cases: gate `Unsupported` → up **not** invoked, banner visible + not
  success; gate `Ok` → up invoked. Update existing constructors to pass a fake `IVersionGate`.

## Decisions to confirm (Q1–Q3)
- **Q1 — Minimum floor value.** agent-eve is `0.0.0` (dev, no released semver).
  - **(A, recommended)** declare `>=0.0.0` as the initial structural floor: the gate is live (it
    refuses checkouts too old to even report a version, i.e. pre-#582 T1), passes all current `0.0.0`
    checkouts, and is bumped to a real semver at the first agent-eve release.
  - (B) a stricter floor like `>=0.1.0` — currently refuses *all* `0.0.0` dev checkouts (too strict
    for daily use).
  - (C) feature-gate only (refuse if `--version` is unsupported, no numeric floor yet).
- **Q2 — Gate scope.**
  - **(A, recommended)** gate only the provision paths (`RunProvisionAsync` + `ResetAndProvisionAsync`),
    matching #24 AC "before provisioning".
  - (B) also gate the Prerequisite `Check` button, so the operator learns the checkout is unsupported
    before prereqs run.
- **Q3 — UI surfacing of a refusal.**
  - **(A, recommended)** operator-fixable red banner, provision does not start.
  - (B) disable the Provision/Reset buttons once the version is known unsupported, with a tooltip.

## Out of scope
- Changing the sandbox manifests; remote/cloud provisioning.
- Removing the (currently UI-unused) `SandboxOrchestrator` beyond the one-line ctor update.
- The end-to-end validation run itself — that is #25. This issue delivers the *guard*; proving it
  against a live checkout is tracked by #25.

## Verification
- `build.yml` (Build & Test, Windows): new + updated unit tests green (keep the suite green; count
  rises from 86 to ~92).
- No local .NET SDK, so CI is the verification gate. No e2e provisioning run here (that is #25).
