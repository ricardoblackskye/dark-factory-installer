# ADR 0003: Binaries are verified, not downloaded

- Status: Accepted
- Date: 2026-11-06
- Workstream: Dark Factory Installer — decision made in #5, recorded here for #16
- Supersedes: none
- See also: [ADR-0004](0004-documentation-update-strategy.md); the manifest at
  `src/DarkFactoryInstaller/resources/artifacts.lock.json`.

## Context

The installer needs certain binaries present to run the Dark Factory toolchain. Two strategies
exist: download them at install time, or verify them against a manifest the program carries.

## Decision

The program **carries the manifest** (`src/DarkFactoryInstaller/resources/artifacts.lock.json`,
copied from agent-eve #603) and **verifies** binaries against it. It **never downloads** binaries at
runtime. Each manifest entry is `acquiredBy: "operator"` today: the program runs the entry's `probe`
command, extracts the version, and checks it against `versionConstraint`; a missing/mismatched binary
surfaces the manifest's own `fix` text. The download + digest-verify path is implemented and
unit-tested but inert until an entry is marked `acquiredBy: "program"` with a `source` URL.

## Consequences

- **Easy:** no runtime network dependency, no supply-chain surface from a download step, and a single
  auditable manifest.
- **Hard:** the manifest must be kept current as binaries change; the doc-drift guard (G4) keeps
  `docs/prerequisites.md` in sync with the committed snapshot.
- **Deferred:** signature/checksum verification mechanics are specified by later workstreams; this ADR
  fixes only the verify-not-download policy.
