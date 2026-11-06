# Plan: #7 — WS5 Orchestration over the headless CLI

**Epic:** #1 (Dark Factory Installer) · **Issue:** [dark-factory-installer#7](https://github.com/ricardoblackskye/dark-factory-installer/issues/7)
**Branch:** `feat/7-orchestration` (off `origin/main`)
**Depends on:** #2, #4, #5, #6 · **Feeds:** #8 (UI), #9 (retry), #10 (errors).

## Context
#7 completes the orchestration loop: drive agent-eve's headless `df:sandbox-up` from the app as
flags-only subprocesses, own the process lifecycle (start/observe/cancel), stream NDJSON stdout vs
prose stderr, honour version gating, and refuse secrets on argv. Scaffolding (`CliRunner`,
`SandboxRunRequest`) already exists from PR #13, and the building blocks are present:
`ISandboxCli.RunCheckAsync` (the `--check` gate, #4), `IClusterCreator` (#6), `BinaryVerifier` (#5), and
the `VersionConstraint` domain type. What's **missing** is the ordered, independently-testable
orchestrator that composes them, and the **streaming provision runner** (AC2/AC3) — `ISandboxCli`
today only runs `--check`; nothing launches the real provision.

## Decision
- Introduce `ISandboxOrchestrator` + `SandboxOrchestrator` as the **single place** that decides the
  pipeline order (AC6): **version gate → prereqs → cluster → check → provision**. Every dependency is an
  interface with a fake, so `RunAsync` is unit-testable without a UI or a real process (AC6).
- Pipeline stages and the primitives they use:
  - **Version gate (AC4)** — new `IVersionGate` runs the CLI version command via `IProcessRunner`, parses
    semver, compares to a `VersionConstraint` (reuse existing domain type; floor from
    `SandboxRunRequest.MinimumCliVersion`, defaulting to a pinned floor). Refuses before any launch if
    unsupported.
  - **Prereqs (#4/#5)** — delegate to the existing local binary-verification primitive (`BinaryVerifier`)
    so minikube/docker/kubectl are confirmed; abort if not satisfied. (Interface extracted if needed for
    the fake.)
  - **Cluster (#6)** — `IClusterCreator.CreateAsync` (default profile `minikube`); capture the kube context
    for the provision argv.
  - **Check (AC1)** — `ISandboxCli.RunCheckAsync` (`df:sandbox-up --check`); if it reports not-ready
    (exit 10 / problems), **do not proceed** to provision.
  - **Provision (AC2/AC3/AC5)** — new `IProvisionRunner.StartAsync(request, sink, ct)` launches
    `df:sandbox-up` (built by `CliRunner.BuildCommand(request, kubeContext)`) as a **streaming** subprocess:
    stdout (NDJSON) → `IOutputSink.OnStdout`, stderr (prose) → `IOutputSink.OnStderr`; returns a
    `ProvisionSession` with `WaitAsync()` (exit code) and `Cancel()` (kills the process). AC5: before
    launch, `ArgvSecretGuard` scans the built argv and refuses (fail-closed, mirrors the CLI's exit-2
    contract) if a secret-like token is present.
- **Streaming primitive (AC2/AC3):** add `IProvisionRunner` **separate** from the capture-based
  `IProcessRunner` used by check/minikube, so streaming + cancellation semantics don't disturb the existing
  #4/#6 tests. Real impl `ProcessProvisionRunner` launches the process with redirected streams and honours
  `CancellationToken` by killing the process.
- **Cancellation (AC2):** orchestrator threads a `CancellationToken` through `RunAsync`; on cancel it calls
  `ProvisionSession.Cancel()` and returns a cancelled result.
- **No secrets on argv (AC5):** `CliRunner.BuildCommand` already emits only `--repo/--issue/--kube-context`,
  so by construction no secret is on argv; `ArgvSecretGuard` is the fail-closed backstop + the explicit
  contract point. Secrets are supplied via vault handle/stdin per #582, never appended here.

## Tasks (TDD — tests before code)
- **T1 — Domain models** `src/DarkFactoryInstaller.Domain/OrchestrationModels.cs`: `OrchestrationStage`
  enum (VersionGate/Prereqs/Cluster/Check/Provision/Cancelled), `OrchestrationResult` record
  (Success, ExitCode?, KubeContext?, Stage, Errors), `VersionGateResult`; add optional
  `VersionConstraint? MinimumCliVersion` to `SandboxRunRequest`.
- **T2 — Streaming sink + provision runner** `IOutputSink.cs`, `IProvisionRunner.cs` (+ `ProvisionSession`),
  and `ProcessProvisionRunner.cs` (real streaming impl over the process launcher).
- **T3 — Version gate** `IVersionGate.cs` + `CliVersionGate.cs` (uses `IProcessRunner` + `VersionConstraint`).
- **T4 — Secret guard** `IArgvSecretGuard.cs` + `ArgvSecretGuard.cs` (scans argv; refuses on secret pattern).
- **T5 — Orchestrator** `ISandboxOrchestrator.cs` + `SandboxOrchestrator.cs` composing the pipeline
  (AC1, AC4, AC5, AC6).
- **T6 — Tests** `tests/DarkFactoryInstaller.Tests/SandboxOrchestratorTests.cs` with fakes
  (`FakeSandboxCli`, `FakeClusterCreator`, `FakeProvisionRunner`, `FakeVersionGate`, `FakeArgvSecretGuard`,
  `RecordingSink`):
  - AC1 — `--check` not-ready ⇒ no provision launched.
  - AC2 — provision launched, can be cancelled (session.Cancel called, result.Cancelled).
  - AC3 — NDJSON lines routed to `OnStdout`, prose to `OnStderr` (via fake provision runner invoking the sink).
  - AC4 — unsupported CLI version ⇒ abort before provision (no provision launched).
  - AC5 — secret on argv ⇒ refused before launch.
  - AC6 — verify order: version gate → prereqs → cluster → check → provision, observable via fake call order.
- **T7 — CI**: tests run under existing `dotnet test` (windows-latest). No new workflow; the #16
  `DocDriftGuards` must remain green (no README/`CliRunner` doc change — `BuildCommand` output unchanged).

## Files
NEW
- `src/DarkFactoryInstaller.Domain/OrchestrationModels.cs`
- `src/DarkFactoryInstaller.Orchestration/IOutputSink.cs`
- `src/DarkFactoryInstaller.Orchestration/IProvisionRunner.cs`
- `src/DarkFactoryInstaller.Orchestration/ProcessProvisionRunner.cs`
- `src/DarkFactoryInstaller.Orchestration/IVersionGate.cs`
- `src/DarkFactoryInstaller.Orchestration/CliVersionGate.cs`
- `src/DarkFactoryInstaller.Orchestration/IArgvSecretGuard.cs`
- `src/DarkFactoryInstaller.Orchestration/ArgvSecretGuard.cs`
- `src/DarkFactoryInstaller.Orchestration/ISandboxOrchestrator.cs`
- `src/DarkFactoryInstaller.Orchestration/SandboxOrchestrator.cs`
- `tests/DarkFactoryInstaller.Tests/SandboxOrchestratorTests.cs`
MODIFY
- `src/DarkFactoryInstaller.Domain/SandboxRunRequest.cs` — add optional `VersionConstraint? MinimumCliVersion`.

## Verification
`build.yml` (windows-latest) green: `dotnet build` + `dotnet test` all pass, including the new
`SandboxOrchestratorTests` and the existing #16 `DocDriftGuards` (no README/`CliRunner` doc change).

## Notes / dependencies to confirm during implementation
- The exact version-command (`df:sandbox-up --version` vs `df --version`) and the default floor will be
  confirmed against the #582 contract when wiring `CliVersionGate`.
- "Prereqs" stage reuses the #4/#5 primitive; if `BinaryVerifier`/`PrerequisiteChecker` lack an interface,
  one is extracted so the orchestrator stays testable.
