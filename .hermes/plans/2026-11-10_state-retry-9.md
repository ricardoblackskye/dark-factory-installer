# Plan: #9 — WS7: State & retry

**Epic:** #1 (Dark Factory Installer) · **Issue:** [dark-factory-installer#9](https://github.com/ricardoblackskye/dark-factory-installer/issues/9)
**Branch:** `feat/9-state-retry` (off `origin/main` @ `2df29d7`)

## Context
The app must persist provisioning progress so the UI can show where things stand and let the operator
retry a failed/interrupted run (#9). agent-eve already does idempotent skip on its side
(`sandbox-state.ts`, #585 T4): it records completed stages + their inputs and skips them on a re-run,
with **transitive** invalidation when an input changes. `df:sandbox-down` leaves minikube up but
releases the namespace/port (#582), so an existing cluster is resumable. The installer's #9 work is to
**surface** that progress in the UI, **offer resume / reset**, and keep its own observed-progress
record **resilient** — it does not re-implement the CLI's skip logic.

### agent-eve contract (grounding)
- `STAGES = vendor | cluster | deploy | forward | proof`; a stage is skipped only when its recorded
  inputs still match (`canSkipStage`). `DEPENDENTS` cascade is transitive (a vendor change invalidates
  deploy → forward → proof), so a stale skip can never masquerade as a proven provision.
- State file `.data/sandbox-provision-state.json` under the workspace; **no secrets** are written
  (only digests, context names, ports). A missing/malformed file is treated as "never provisioned"
  rather than fatal.
- `df:sandbox-down` (no flag) tears down release + namespaces, leaves minikube; `--delete-cluster`
  is the full reset.

## Decision (confirmed direction)
- The installer keeps a **lightweight own record** (observed step completion + last outcome + timestamp)
  per workspace/target, persisted under `%LOCALAPPDATA%/DarkFactoryInstaller/<targetId>/provision-state.json`.
  This survives app relaunch and drives the UI progress view. It contains **no secrets**.
- **Resume** re-runs `df:sandbox-up` (CLI skips completed stages via its own `.data` state, aligned by
  passing `--workspace-dir`). **Reset & re-provision** runs `df:sandbox-down --delete-cluster` then
  `df:sandbox-up` (clean). Both reuse the #8 streaming seam.
- Writes are **atomic** (temp file + move) so an app-close mid-run cannot leave a corrupt/blocking file.

## Tasks (TDD, AC1–AC5)

- **T1 — Domain.** `ProvisionState` (`TargetId`, `CompletedSteps` set, `Outcome` enum
  `InProgress|Succeeded|Failed`, `Timestamp`); no secret-bearing fields (AC1/AC4).
- **T2 — Orchestration.** `IProvisionStateStore` + `FileProvisionStateStore`: load (malformed → empty,
  never blocks, AC5), atomic save, isolated per `<targetId>` (AC4). Unit-tested with a fake/temp dir.
- **T3 — Orchestration.** Extend the #8 orchestration to invoke `df:sandbox-down` (+ `--delete-cluster`)
  for the reset path, reusing `IStreamingProcessRunner`; pass `--workspace-dir` to `df:sandbox-up` so the
  CLI's skip state lines up with the installer's target (AC3). Fake runner for tests.
- **T4 — WPF.** Extend #8 `ProvisionViewModel` / `MainWindow`: on launch, load persisted state and show
  last progress; expose **Resume** and **Reset & re-provision** commands; the step list + banner stay as
  #8 built them (AC2).
- **T5 — Tests.** `ProvisionStateStoreTests` (round-trip, isolation per target, malformed-tolerated,
  atomic-write resilience), `SandboxDownRunnerTests` (reset/clean flags), and `ProvisionViewModelTests`
  (resume vs reset, last-progress shown). All with fakes — no live cluster / real CLI.

## Files
NEW (Domain): `src/DarkFactoryInstaller.Domain/ProvisionState.cs`
NEW (Orchestration): `src/DarkFactoryInstaller.Orchestration/IProvisionStateStore.cs`, `FileProvisionStateStore.cs`, `SandboxDownRunner.cs` (or extend the #8 orchestrator)
MODIFY (Orchestration): the #8 streaming orchestrator — add `df:sandbox-down` + `--workspace-dir` pass-through
MODIFY (WPF): `ProvisionViewModel.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` — load state, Resume/Reset UI
NEW (Tests): `tests/DarkFactoryInstaller.Tests/ProvisionStateStoreTests.cs`, `SandboxDownRunnerTests.cs`

## Verification
- `dotnet test` on `windows-latest` (`build.yml`) is **green**: state store + reset + viewmodel unit-tested
  with fakes; no live cluster or real CLI required. CI fails if a guard drifts.
- Manual (optional): run a provision, close mid-run, relaunch → last progress shown; Resume skips, Reset cleans.

## Open questions
- Whether to additionally read agent-eve's `.data/sandbox-provision-state.json` directly to reflect the
  CLI's true skip state. The installer's own observed-progress record is authoritative for the UI; reading
  the CLI file is a possible enrichment — decide during implementation.

## References
Epic #1; issue #9. Depends on #7 (steps/streaming, merged), #6 (cluster lifecycle, merged), #8 (NDJSON UI,
merged). agent-eve `sandbox-state.ts` (#585 T4), `sandbox-up.ts` / `sandbox-down` (CLI contract #582),
`dark-factory-local-runs.md`. Deferred decisions: #11 (signing), #12 (audience).
