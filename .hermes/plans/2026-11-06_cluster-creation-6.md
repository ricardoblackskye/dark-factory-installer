# Plan: #6 — Cluster creation (program-side)

**Epic:** #1 (Dark Factory Installer) · **Issue:** [dark-factory-installer#6](https://github.com/ricardoblackskye/dark-factory-installer/issues/6)
**Branch:** `feat/6-cluster-creation` (off `origin/main`)
**Depends on:** #2 (shell), #4 (prereq detection). Feeds #7 (orchestration), #8 (UI), #9 (retry).
**Part of:** epic #1.

## Context

Per the #584 boundary, this repo *owns* cluster creation; agent-eve's `df:sandbox-up` provisions
*into* an existing cluster via `--kube-context` and does not create one. #6 adds the program-side
cluster lifecycle: create (minikube), capture the resulting kube context, pass it to the CLI, report
failures with actionable guidance, and honor the operator's teardown preference.

## Decision

- Cluster ops sit behind an `IClusterCreator` interface driven by `IProcessRunner` (already injectable;
  mirrors `ISandboxCli` / `PrerequisiteChecker`), so the logic is unit-testable with a fake process
  runner — no real minikube/Docker in CI.
- `minikube start -p <profile>` creates the cluster; the kube context is parsed from stdout
  (`kubectl context will be "<ctx>"`) and falls back to the profile name. The captured context is passed
  to `df:sandbox-up` via `--kube-context`, added as an **optional** parameter to `CliRunner.BuildCommand`
  so the existing 1-arg form — and the #16 doc-drift guard — is unchanged.
- Teardown: `LeaveUp` → `minikube stop -p <profile>` (releases ports/namespace, keeps cluster, matching
  `df:sandbox-down` semantics); `TearDown` → `minikube delete -p <profile>`.
- Failures (non-zero exit, or runner exception) are caught and returned as `ClusterCreationResult` /
  `ClusterTeardownResult` carrying the stderr `problems` as actionable errors — never a raw crash (AC3).
- AC5/AC6 wiring into UI (#8) and orchestration gating (#7, after #4) is out of scope here; #6 just
  exposes `ClusterCreationResult` (context + `ClusterState`) for those workstreams to consume.

## Tasks (TDD — tests before code)

- **T1 — Domain models** `src/DarkFactoryInstaller.Domain/ClusterModels.cs`: `ClusterState` enum,
  `ClusterTeardownPreference` enum, `ClusterCreationResult`, `ClusterTeardownResult`, `ClusterInfo`.
- **T2 — CliRunner kube-context (AC2)** add optional `string? kubeContext = null` to
  `CliRunner.BuildCommand`; the 1-arg form is unchanged.
- **T3 — IClusterCreator + MinikubeClusterCreator** `src/DarkFactoryInstaller.Orchestration/`: interface
  + impl over `IProcessRunner` (create / teardown, context capture, error handling).
- **T4 — Tests** `tests/DarkFactoryInstaller.Tests/ClusterCreatorTests.cs` with a fake `IProcessRunner`:
  - create runs `minikube start`, captures context from stdout (AC1);
  - default profile = `minikube` when none given;
  - non-zero exit / runner exception → failure with stderr errors (AC3);
  - `LeaveUp` → `minikube stop`, `TearDown` → `minikube delete` (AC4);
  - `CliRunner.BuildCommand(req, "darkfactory")` contains `--kube-context darkfactory` (AC2) and the
    1-arg form still equals `df:sandbox-up --repo owner/repo --issue 1` (regression guard for #16).
- **T5 — CI** no new workflow; tests run under the existing `dotnet test` (windows-latest).

## Files

NEW
- `src/DarkFactoryInstaller.Domain/ClusterModels.cs`
- `src/DarkFactoryInstaller.Orchestration/IClusterCreator.cs`
- `src/DarkFactoryInstaller.Orchestration/MinikubeClusterCreator.cs`
- `tests/DarkFactoryInstaller.Tests/ClusterCreatorTests.cs`

MODIFY
- `src/DarkFactoryInstaller.Orchestration/CliRunner.cs` — optional kube-context parameter (AC2).

## Verification

`build.yml` (windows-latest) green: `dotnet build` + `dotnet test` all pass, including the new
`ClusterCreatorTests` and the existing `DocDriftGuards` (#16) — confirming the `CliRunner` change did
not break the doc-drift guard.
