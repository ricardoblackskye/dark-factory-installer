# Plan: #5 — Binary acquisition + verify (WS3)

**Epic:** #1 (Dark Factory Installer) · **Workstream:** WS3 · **Issue:** [dark-factory-installer#5](https://github.com/ricardoblackskye/dark-factory-installer/issues/5)
**Branch:** `feat/5-binary-acquisition` (off `origin/main`)
**Prerequisite:** #2 (scaffold) ✅ merged. **Consumed by:** #7 (orchestration), #8 (UI), #10 (errors).

---

## Context

The Windows program must ensure the headless CLI's prerequisites (docker, kubectl, helm, python,
minikube) are present and correct **before** any provisioning begins, and verify their integrity.
The single source of truth is agent-eve's manifest `scripts/sandbox-vendor/artifacts.lock.json`
(defined in **#603**). This workstream **consumes** that manifest — it MUST NOT re-implement
acquisition or vendor its own copy of the binaries (AC5). It is the program-side counterpart to
#603's refuse-early: #603 makes the CLI *refuse* on a missing/mismatched binary (exit 10,
`prerequisites-missing` / `prerequisites-mismatch`); #5 makes the *program* acquire/verify up front
so that refusal never happens mid-deploy.

> Note on relationship to #4: #4 added a lightweight pre-flight gate by delegating to
> `df:sandbox-up --check` (exit-code + stderr prose). #5 is the **program-owned, manifest-driven**
> acquisition/verification that produces a richer `BinaryStatus[]` the UI shows and #7 consumes
> before provisioning. The two layers coexist: #5 verifies from the manifest directly; #7 still
> runs the CLI `--check` as a final guard.

### What the manifest actually contains (from #603)
`artifacts.lock.json` has `chart`, `crds`, `images[]` (sandbox artifacts, downloaded by the CLI's
own `df:sandbox-vendor`), and `binaries[]`. Each binary entry:
```
{ "name", "versionConstraint" (e.g. ">=20.10.0"), "required" (bool),
  "requiredFor" (optional, e.g. "minikube"), "acquiredBy" ("operator"),
  "probe": { "command", "args" }, "fix" (actionable message) }
```
**Key fact:** every current binary is `"acquiredBy": "operator"` — i.e. the manifest expects the
*operator* to install system CLIs; it does not carry a download `source`. So for the current
manifest the program **verifies** operator-provided installs and reports the `fix` text when
missing; it does **not** download docker/kubectl/helm/python/minikube. The download + digest path
is built and unit-tested but inert until a future manifest entry marks a binary `acquiredBy:
"program"` with a `source` URL + `expectedDigest`.

---

## Decision (recommend)

1. **Carry a committed copy of the manifest**, not the binaries.** `resources/artifacts.lock.json`
   is a snapshot of agent-eve's lock, refreshed when the upstream manifest changes. The manifest
   remains the single source of truth in agent-eve; the copy is a lockfile (like agent-eve itself
   commits it). AC5 forbids vendoring *binaries* — a manifest copy is explicitly allowed.
2. **Manifest-driven, not hard-coded.** Discover binaries from the manifest. Run each entry's
   `probe` (`command` + `args`) via an injected `IProcessRunner`; extract the **first SemVer token**
   from the output (works for all 5 current tools: docker `20.10.24`, kubectl `Client Version:
   v1.29.0` → `1.29.0`, helm `v3.14.0`, python `Python 3.12.1`, minikube `v1.32.0`); compare against
   `versionConstraint` (`>=`, `>`, `==`, `<=`, `<` — current manifest only uses `>=`).
3. **Acquisition is conditional on `acquiredBy`.** `operator` → verify + report `fix`. `program` →
   download from the named `source`, verify sha256 against `expectedDigest`, fail hard on mismatch.
4. **Optional binaries don't block by default.** `required:false` (minikube) is reported but does
   not block provisioning unless the selected cluster mode (`requiredFor`, wired in #6/#7) needs it.
5. **Digest verification (AC3)** is implemented for any entry carrying `expectedDigest`. Current
   `binaries[]` carry none, so it is exercised via tests + future manifest entries (the `images[]`
   digests are consumed by the CLI, not the program).

---

## Tasks (TDD — tests before code, fakes for process/network)

- **T1 — Manifest model + loader** (AC1)
  `Domain/BinaryManifest.cs` (`BinaryManifest`, `BinaryEntry`, `ProbeSpec`) parsed from JSON.
  `Orchestration/IBinaryManifestSource` + `FileBinaryManifestSource` (reads `resources/…json`).
  Test fake returns an in-memory manifest. Assert parsing of `versionConstraint`/`required`/`probe`.
- **T2 — Version verification** (AC2)
  `Orchestration/BinaryVerifier` runs `probe` via `IProcessRunner` (injected; fake returns version
  strings), extracts first SemVer token, compares to constraint → `BinaryStatus` (Present / Missing /
  Mismatch). Small `SemVer` + `VersionConstraint` comparer (unit-tested: `>=20.10.0` with `20.9.0`
  → Mismatch, `20.10.0`/`20.11.0` → Present).
- **T3 — Digest verification** (AC3)
  `Orchestration/BinaryDownloader` with `IFileDownloader` (fake for tests). When an entry has
  `expectedDigest`, compute sha256 of the fetched file; mismatch → hard failure with actionable
  message. Test: matching digest passes; mismatched fails.
- **T4 — Acquisition service** (AC2/AC4/AC6)
  `Orchestration/BinaryAcquisitionService` orchestrates load → for each entry: `operator` → verify;
  `program` → download + verify. Produces `BinaryStatus[]`. Honours `required` vs `requiredFor`
  (optional reported, not blocking). Exposes `IBinaryManifestSource` so tests inject a manifest.
- **T5 — Failure surfacing** (AC4)
  Missing/Mismatch → message taken from the manifest `fix` (no re-invention, mirrors #603
  refuse-early). Surfaced before provisioning, never as a mid-deploy crash.
- **T6 — Expose state** (AC7)
  `BinaryStatus[]` exposed for the UI (#8) and error handling (#10). Minimal MainWindow hook now
  (list the statuses); full step-list UI is #8.
- **T7 — Decision record**
  README/ADR note: committed manifest copy (not binaries); acquire-vs-verify split on `acquiredBy`;
  version extraction = first SemVer token.

---

## Files

NEW
- `src/DarkFactoryInstaller.Domain/BinaryManifest.cs` — `BinaryManifest`, `BinaryEntry`, `ProbeSpec`.
- `src/DarkFactoryInstaller.Domain/BinaryStatus.cs` — `BinaryStatus` (+ `BinaryState` enum).
- `src/DarkFactoryInstaller.Orchestration/IBinaryManifestSource.cs` + `FileBinaryManifestSource`.
- `src/DarkFactoryInstaller.Orchestration/BinaryVerifier.cs` (+ `IProcessRunner`, `SemVer`, `VersionConstraint`).
- `src/DarkFactoryInstaller.Orchestration/BinaryDownloader.cs` (+ `IFileDownloader`).
- `src/DarkFactoryInstaller.Orchestration/BinaryAcquisitionService.cs`.
- `resources/artifacts.lock.json` — committed manifest snapshot (from agent-eve #603).
- `tests/DarkFactoryInstaller.Tests/BinaryAcquisitionTests.cs` — verifier / downloader / service with fakes.

MODIFY
- `src/DarkFactoryInstaller/MainWindow.xaml` + `.cs` — light: bind a binary-status list (full UI = #8).
- `README.md` — decision note (T7).

---

## Verification

- `dotnet test` on `windows-latest` (build.yml): verifier, downloader (fake `IFileDownloader`, no
  network), and service (fake `IBinaryManifestSource`) tests. Manifest-snapshot parse test.
- No real binary download or system-tool probe in CI — all external I/O is behind fakes.
- Manual local check (optional, needs the tools installed): run the acquisition service against the
  committed snapshot to see `BinaryStatus[]`.

---

## Open questions for review

- **Q1 — Manifest source:** committed snapshot (`resources/`) vs fetch-from-agent-eve at runtime.
  **Recommend committed snapshot** (self-contained, offline, like agent-eve's own committed lock).
  Needs your call.
- **Q2 — Auto-install:** for `acquiredBy:"operator"` binaries that are missing, should the program
  also attempt a best-effort auto-install (winget/choco) or only verify + report `fix`? AC5 implies
  download only when the manifest names a `source`. **Recommend verify + report only for now**
  (auto-install is a later enhancement, possibly its own issue).
- **Q3 — Cluster binary `requiredFor`:** minikube's `requiredFor:"minikube"` gates it on the cluster
  mode chosen in #6/#7. #5 reports it; the gating logic lands in #6/#7. Flagged, not implemented here.
