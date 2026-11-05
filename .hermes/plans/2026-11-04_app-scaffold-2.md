# #2 — App scaffold / .NET 8 setup

- **Epic:** #1 (Dark Factory Installer)
- **Status:** planning — needs review/approval before implementation.
- **Author:** @senior-software-engineer

## Goal
Stand up the Windows desktop application skeleton that becomes the Dark Factory Installer: a runnable
**.NET 8** desktop app with a clear project structure, a build that produces the binary, packaging for
distribution, and CI. This is the foundation every other workstream (#4–#12) builds on. It must **not**
implement installer logic yet, and must **not** re-implement provisioning (agent-eve's `df:sandbox-up`
owns that per #584).

## Why
All later workstreams (prerequisite detection, orchestration, UI step list, signing) assume a working
.NET 8 desktop project with a sensible layering. Establishing the shell + framework choice + toolchain
first prevents rework when features land.

## Scope
**In**
- .NET 8 solution + project at repo root, builds from a clean checkout.
- Desktop framework chosen (WPF or WinUI 3) and recorded with rationale.
- Minimal launchable shell (placeholder UI acceptable).
- Packaging configured to emit a distributable artifact (self-contained publish; signing deferred to #11).
- CI (GitHub Actions) builds on Windows runners.
- README + framework decision documented.
- Project structure separating CLI-runner / UI / domain layers (placeholder folders OK).

**Out**
- Any installer/provisioning logic (#4–#12).
- Invoking `df:sandbox-up` (that is #7).
- Code signing (#11) and the distribution/audience decisions (#12).

## Approach
1. **Framework decision.** Recommend **WPF** for the first cut: lowest-friction, most broadly supported
   .NET 8 desktop framework on Windows, largest ecosystem, avoids the WinUI 3 project/SDK friction
   (separate WindowsAppSDK, stricter toolchain). Record the decision + rationale; note WinUI 3 as a future
   option. (Recommendation only — the user can choose WinUI 3 at review; T1 adapts.)
2. **Project layout.** Single solution with three projects:
   - `src/Installer.App` — WPF UI host (.NET 8).
   - `src/Installer.Core` — domain/model + future CLI-runner types (placeholder now).
   - `src/Installer.CliRunner` — the layer that will shell out to `df:sandbox-up` (placeholder interface
     now; implemented in #7).
   Layering now sets the boundary for later workstreams even if empty.
3. **Build.** `dotnet build` clean from a fresh clone (only the .NET 8 SDK required).
4. **Packaging.** Add a publish profile / CI step producing a self-contained single-file exe
   (`dotnet publish -c Release -r win-x64 --self-contained`) so a distributable artifact exists; signing
   wired in #11.
5. **CI.** GitHub Actions `build.yml` on `windows-latest`: restore → build → publish → upload artifact.
   Blocks merge on failure.
6. **Docs.** README documents local build/run + the framework decision.

## Tasks (acceptance criteria map to #2 AC1–AC7)
- **T1 — Solution + projects (AC1, AC2, AC3, AC7).** Create `dark-factory-installer.sln` with
  `src/Installer.App` (WPF, .NET 8) and the `Core`/`CliRunner` projects. App launches to a minimal
  `MainWindow` (placeholder content). Vertical slices: sln → App project → confirm `dotnet build` + app runs.
- **T2 — Framework decision recorded (AC2, AC6).** Decide WPF vs WinUI 3 (recommend WPF); record in README
  with rationale.
- **T3 — Packaging (AC4).** Add publish config emitting a self-contained win-x64 artifact; verify locally
  with `dotnet publish`.
- **T4 — CI (AC5).** `build.yml` on `windows-latest`: restore → build → publish → upload artifact. Green on a
  fresh push.
- **T5 — README + structure (AC6, AC7).** README with build/run instructions + framework decision; confirm
  the three-layer folder structure exists.

## Verification
- Fresh clone, `dotnet build` succeeds with only the .NET 8 SDK installed.
- `dotnet run --project src/Installer.App` opens the shell window.
- `dotnet publish -c Release -r win-x64 --self-contained` produces an exe.
- CI run is green; artifact downloadable.
- (Optional) `cspell`/markdown lint on docs if the repo adopts the agent-eve guard.

## References
- Issue #2 (ACs AC1–AC7); epic #1.
- Boundary: agent-eve #584; CLI contract #582 / #585 / #603.
- Deferred: signing #11, audience #12, event schema agent-eve #612.
