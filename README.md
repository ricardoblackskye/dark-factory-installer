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
| Orchestration | `src/DarkFactoryInstaller.Orchestration` | Drives `df:sandbox-up` (e.g. `CliRunner`). |
| UI | `src/DarkFactoryInstaller` | WPF shell (placeholder in WS1). |
| Tests | `tests/DarkFactoryInstaller.Tests` | xUnit suite for Domain + Orchestration. |

## CI

`.github/workflows/build.yml` builds and tests the solution on `windows-latest` for every
push/PR; a failing build blocks merge (AC5).
