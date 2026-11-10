# Dark Factory Installer

Windows desktop application + GUI that orchestrates the Dark Factory toolchain (Phase 2 of
[#584](https://github.com/ricardoblackskye/agent-eve/issues/584)). This repository owns the
GUI and orchestration only; it does **not** re-implement provisioning — that stays in
agent-eve's headless `df:sandbox-up` (delivered in #582 / #585 / #603).

> Part of the [Dark Factory Installer epic (#1)](https://github.com/ricardoblackskye/dark-factory-installer/issues/1).
> This repo is the work for **WS1: App scaffold / .NET 8 setup (#2)**.

## Framework decision

**WPF** (`.NET 8`, `net8.0-windows`). See
[`docs/adr/0001-framework-choice.md`](docs/adr/0001-framework-choice.md) for the full
rationale. WinUI 3 was considered and deferred; the UI layer is isolated so a future
refresh would stay local.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the repo pins the 8.0.4xx
  feature band via `global.json`; `dotnet --version` must report an 8.0.x).
- Windows (the application is Windows-only by design — #584).

## Build

```bash
dotnet build            # builds the solution from the repo root
dotnet test             # runs the unit tests (Domain + Orchestration)
```

## Run

```bash
dotnet run --project src/DarkFactoryInstaller
```

### Launch a sandbox run

The installer drives agent-eve's headless `df:sandbox-up`. The canonical invocations below are produced
by `CliRunner` and wrapped by `CliCommands` with the `npm run` host and a per-target `--workspace-dir`
(issue #9). The doc-drift guard (`tests/DarkFactoryInstaller.Tests/DocDriftGuards.cs`) fails CI if they
ever drift from the code:

```bash
df:sandbox-up --check
df:sandbox-up --repo owner/repo --issue 1
```

- `df:sandbox-up --check` — prerequisite gate (exit `0` ready / `10` not ready; remediation on stderr).
- `df:sandbox-up --repo <owner/repo> --issue <n>` — launches a sandbox run.

This launches the placeholder shell (AC3). Installer logic is intentionally out of scope
for WS1; later workstreams (#4–#12) add features behind this build.

## Prerequisite detection (#4)

The app detects prerequisites by delegating to the headless CLI's `df:sandbox-up --check`
rather than probing each tool directly. Reasons (issue #4):

- The CLI already encodes the version constraints and the refuse-early behaviour (#603); delegating
  keeps a single source of truth and stays in sync with agent-eve.
- `df:sandbox-up --check` exits `0` when ready and `10` when a prerequisite is missing/mismatched,
  and writes the actionable `problems` (subject + fix) to stderr as prose; stdout is NDJSON events.
  The app branches on the **exit code** and surfaces remediation from the stderr channel, so it never
  parses the NDJSON event stream for status (per the #582 contract).
- Direct probing of each tool is intentionally **not** done; it remains a fallback only if `--check`
  cannot run pre-install.

## Package (distribute)

A self-contained Windows executable is produced by publishing (AC4; signing deferred to
WS9 #11):

```bash
dotnet publish src/DarkFactoryInstaller -c Release -r win-x64 --self-contained true -o publish
```

The `WinX64SelfContained` publish profile under
`src/DarkFactoryInstaller/Properties/PublishProfiles/` carries the same settings.

## Project structure

Concerns are separated so later workstreams have clear homes (AC7):

| Layer | Project | Responsibility |
| --- | --- | --- |
| Domain / model | `src/DarkFactoryInstaller.Domain` | Value types + validation (e.g. `SandboxRunRequest`). |
| Orchestration | `src/DarkFactoryInstaller.Orchestration` | Drives `df:sandbox-up` (`CliRunner`, `CliCommands`, `ProvisionViewModel`). |
| State / retry | `src/DarkFactoryInstaller.Orchestration` + `Domain` | Per-target persisted progress (`ProvisionState`, `IProvisionStateStore`, `FileProvisionStateStore`). |
| UI | `src/DarkFactoryInstaller` | WPF shell (placeholder in WS1). |
| Tests | `tests/DarkFactoryInstaller.Tests` | xUnit suite for Domain + Orchestration. |

## CI

`.github/workflows/build.yml` builds and tests the solution on `windows-latest` for every
push/PR; a failing build blocks merge (AC5).

## Binary acquisition (#5)

The app verifies the headless CLI's required binaries against agent-eve's committed manifest
(`src/DarkFactoryInstaller/resources/artifacts.lock.json`, copied from agent-eve #603 — the program
carries the manifest snapshot, never the binaries themselves). Decisions (issue #5):

- Binaries are **verified, not blindly downloaded**. The manifest marks every current binary
  `acquiredBy: "operator"`, so the program runs each entry's `probe` command, extracts the version,
  and checks it against `versionConstraint`; a missing/mismatched binary surfaces the manifest's own
  `fix` text (mirroring #603 refuse-early). The download + digest-verify path is implemented and
  unit-tested but inert until a manifest entry is marked `acquiredBy: "program"` with a `source` URL.
- Optional binaries (e.g. `minikube`, `requiredFor: "minikube"`) are reported but do not block
  provisioning unless the operator selects the matching cluster mode (#6/#7).

## Documentation

Architecture decisions live in [`docs/adr/`](docs/adr/README.md) as Architecture Decision Records.
The documentation-update convention — *documentation changes accompany the code they describe, in the
same PR* — is recorded in
[ADR-0004](docs/adr/0004-documentation-update-strategy.md) and enforced by the doc-drift guard in
`tests/DarkFactoryInstaller.Tests/DocDriftGuards.cs` (CI fails on drift).
- Acquisition runs before provision orchestration (#7) so a missing binary blocks up front.

## State & retry (#9)

The installer persists the observed provisioning progress **per target** (e.g. `owner/repo/1`) so the
UI can show the last known state on relaunch or after a failure, and supports a clean reset:

- **Persisted progress** — `ProvisionState` (target id, which stages completed, last outcome, timestamp)
  is written atomically to a file under `%LOCALAPPDATA%/DarkFactoryInstaller/state/<target>.json`. No
  secrets are stored. A missing or malformed file is treated as "never run" and never blocks a
  provision (issue #9, AC1/AC4/AC5).
- **Resume** — re-running `Provision` invokes `df:sandbox-up` again; the passed `--workspace-dir` lets
  the CLI's own idempotent skip resume from where it left off, and the installer re-streams live events
  (issue #9, AC2/AC3).
- **Reset & re-provision** — tears the cluster down fully (`df:sandbox-down --delete-cluster`), clears
  the persisted progress, then re-runs `df:sandbox-up` for a clean baseline (issue #9, AC3).
