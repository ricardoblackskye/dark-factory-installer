# ADR 0004: Documentation-update strategy (Option A)

- Status: Accepted
- Date: 2026-11-06
- Workstream: Dark Factory Installer — issue #16
- Supersedes: none
- See also: agent-eve ADR-0012 ("docs reference pages are generated at render time") and the #238
  doc-drift-guard tests.

## Context

Architecture decisions were captured inline in `README.md`, which made them hard to find and easy to
let drift. We considered two ways to keep docs honest:

- **Option A (chosen):** hand-maintained ADRs under `docs/adr/` plus xUnit doc-drift-guard tests that
  run in the standard `dotnet test` CI job and **fail the build on documentation drift**.
- **Option B (rejected):** build-time generation + a committed snapshot diff. Heavier, and weaker here
  — there is no web render pipeline to regenerate pages, and ADR-0012 discourages committing generated
  snapshots.

## Decision

Adopt **Option A**. Architecture decisions live as ADRs in `docs/adr/`; the `DocDriftGuards` xUnit class
(in `tests/DarkFactoryInstaller.Tests`) fails CI when the docs drift from the code/manifest they describe.

**Convention:** documentation changes ship in the **same PR as the code they describe**. Drift fails CI;
it does not auto-open a PR, and we do **not** commit generated snapshots.

## Consequences

- **Easy:** drift is caught by the existing `dotnet test` job on `windows-latest` — no extra pipeline.
- **Easy:** every guard has a mutation test proving it fires on synthetic drift (AC5), so the guards are
  not vacuous.
- **Hard:** authors must update docs alongside code; the same-PR convention is a discipline, not an
  enforced gate (it is recorded here and referenced from `README.md`).
- **Deferred:** a render-time docs pipeline (mirroring agent-eve ADR-0012) remains possible later, but
  is unnecessary without a web surface.
