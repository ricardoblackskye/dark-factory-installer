# ADR 0001: Desktop UI framework — WPF

- Status: Accepted
- Date: 2026-11-05
- Workstream: WS1 (App scaffold / .NET 8 setup) — issue #2
- Supersedes: none

## Context

WS1 must stand up a runnable .NET 8 desktop application that becomes the Dark Factory
Installer. The issue (AC2) requires choosing **WPF or WinUI 3** and recording the rationale.
The chosen framework is used consistently by every later workstream (WS2–WS12), so the
decision is load-bearing: reversing it later means rewriting all UI.

Forces in play:

- The shell must build cleanly with `dotnet build` from a clean checkout and on GitHub
  Actions `windows-latest` on every push (AC1, AC5) — with no assumptions beyond the
  .NET 8 SDK being installed.
- This workstream establishes the shell only; it deliberately does **not** implement
  installer logic (per #584 boundary: provisioning stays in agent-eve's headless
  `df:sandbox-up`, delivered in #582 / #585 / #603).
- The installer's UI is functional, not visually rich: prerequisite detection (WS2),
  progress, logs, and a launch button for `df:sandbox-up`. It does not need the modern
  styling or rounded-control system that motivates WinUI 3.

## Decision

Use **WPF** (Microsoft.NET.Sdk.WindowsDesktop, `net8.0-windows`, `<UseWPF>true</UseWPF>`)
as the desktop framework for the Dark Factory Installer.

All UI work in WS2–WS12 builds on WPF/XAML.

## Consequences

- **Easy:** `dotnet build` works with the stock .NET 8 SDK on Windows — WPF's Windows
  Desktop workload ships with the SDK on Windows hosts; CI needs only `actions/setup-dotnet`
  with `dotnet-version: 8.x`, no extra workload install or Windows App SDK bootstrap.
- **Easy:** a self-contained `.exe` and MSIX are both reachable from a WPF project
  (AC4); MSIX signing is deferred to WS9 (#11) regardless of framework.
- **Hard / impossible:** adopting WinUI 3's visual style without switching frameworks.
  This is accepted — the installer does not need it, and WS9 can revisit packaging
  (MSIX) on top of the same WPF project if desired.
- **Hard / impossible:** running on non-Windows. The installer is Windows-only by #584's
  definition; both WPF and WinUI 3 are Windows-only, so this is neutral between the options.

## Deferred

- WinUI 3 / Windows App SDK remains a candidate for a future visual refresh, but not for
  the WS1 scaffold. Revisiting it would be a separate ADR and would require migrating the
  UI layer — which is why the UI layer is isolated behind `src/DarkFactoryInstaller` and
  depends only on the Domain/Orchestration layers (AC7), keeping any future migration local.
