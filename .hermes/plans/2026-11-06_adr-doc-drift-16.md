# Plan: #16 — ADR documentation + doc-drift guards (Option A)

**Epic:** #1 (Dark Factory Installer) · **Issue:** [dark-factory-installer#16](https://github.com/ricardoblackskye/dark-factory-installer/issues/16)
**Branch:** `feat/16-adr-doc-drift` (off `origin/main`)
**Depends on:** #2 (scaffold, merged), #4 (prereq detection, merged), #5 (binary acquisition, merged). **Part of:** epic #1.

---

## Context

Issue #16 asks to add ADR documentation "the same as we have with the Eve Agent". agent-eve's
auto-documentation is two legs (ADR-0012 + #238): (1) mechanical reference pages **generated at render
time** (never committed, so they can't drift), and (2) **doc-drift-guard tests** that fail CI on drift.

This repo has **no web render pipeline**, so leg (1) doesn't translate. We chose **Option A** (confirmed
with the user): hand-maintained ADRs + an xUnit `DocDriftGuards` class that runs in the standard
`dotnet test` CI job and **fails the build on documentation drift**. Convention: *documentation changes
in the same PR as the code it describes* — drift fails CI, it does not auto-open a PR, and we do not
commit generated snapshots (ADR-0012 discourages committed generated pages; there is no pipeline to
regenerate them). This is the faithful, portable half of Eve's method.

---

## Decision (Option A)

- ADRs live in `docs/adr/` with an `README.md` index; they are hand-written.
- `DocDriftGuards` enforces that docs stay in sync with source; it runs in `dotnet test` (already part of
  `build.yml`), so drift fails CI automatically.
- No auto-PR, no generated/committed markdown.

---

## Tasks (TDD — tests before code)

- **T1 — ADR scaffold (AC1)** `docs/adr/README.md` index listing every ADR.
- **T2 — Seed ADRs (AC2)**
  - `0001-framework-choice.md` — WPF / .NET 8 for the Windows shell.
  - `0002-prereq-delegates-to-cli.md` — prerequisite detection delegates to `df:sandbox-up --check`.
  - `0003-binaries-verified-not-downloaded.md` — binaries verified, not downloaded; program carries the
    manifest, never the binaries (#5 decision).
  - `0004-documentation-update-strategy.md` — records Option A + the same-PR convention (this decision).
- **T3 — DocDriftGuards (AC3/AC4)** in `tests/DarkFactoryInstaller.Tests/DocDriftGuards.cs`. Repo root is
  found by walking **up** from `AppContext.BaseDirectory` to `dark-factory-installer.sln` (robust in CI
  and locally). Guards:
  - **G1 no orphan ADRs** — every `docs/adr/*.md` (excluding the index) is linked from `docs/adr/README.md`.
  - **G2 no dead internal links** — every relative `(...)(file.md)` in `docs/**.md` resolves on disk.
  - **G3 README reflects the CLI surface** — README mentions both command forms the code emits:
    `CliRunner.BuildCheckCommand()` → `df:sandbox-up --check`, and `CliRunner.BuildCommand(req)` →
    `df:sandbox-up --repo … --issue …`. Assert both substrings are present.
  - **G4 prerequisites doc matches the manifest** — `docs/prerequisites.md` enumerates every binary +
    `versionConstraint` from `resources/artifacts.lock.json` (parsed via the Domain `BinaryManifest`
    model). Assert each `name` + `versionConstraint` string from the manifest appears in that doc.
- **T4 — Mutation tests (AC5)** each guard has a synthetic-drift case proving it fires: inject a missing
  ADR link (G1), a broken link (G2), a README with the CLI mention stripped (G3), a prerequisites doc
  with one binary removed (G4). Mirrors agent-eve #238 (a vacuous guard is worse than none).
- **T5 — Convention wiring (AC6)** ADR-0004 records the same-PR convention; `README.md` links `docs/adr/`
  and the convention. New workstreams are expected to add an ADR when they make an architecture decision.
- **T6 — CI** no new workflow: the guards run under the existing `dotnet test` step in `build.yml`, so
  drift fails CI by construction.

---

## Files

NEW
- `docs/adr/README.md`
- `docs/adr/0001-framework-choice.md`
- `docs/adr/0002-prereq-delegates-to-cli.md`
- `docs/adr/0003-binaries-verified-not-downloaded.md`
- `docs/adr/0004-documentation-update-strategy.md`
- `docs/prerequisites.md`
- `tests/DarkFactoryInstaller.Tests/DocDriftGuards.cs`

MODIFY
- `README.md` — link to `docs/adr/` + note the same-PR documentation convention.

---

## Verification

- `dotnet test` on `windows-latest` (build.yml): `DocDriftGuards` (G1–G4 + mutation tests) run with the
  suite; a documentation drift fails CI.
- The guards scan the real repo (not mocks), so they are integration tests over the checked-out tree.
- No network / no real binaries required.

---

## Open questions

- None blocking. Link/dead-link extraction regexes will be kept tolerant (skip external `http(s)://` and
  in-page `#anchors`, as agent-eve does).
