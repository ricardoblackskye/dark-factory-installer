# #4 — Prerequisite detection

- **Epic:** #1 (Dark Factory Installer)
- **Depends on:** #2 (app shell) — done (merged via PR #13)
- **Status:** planning — needs review/approval before implementation.
- **Author:** @senior-software-engineer

## Goal
Detect whether the operator's machine satisfies the prerequisites the agent-eve CLI needs, and surface what
is missing or wrong-version **before** any provisioning is attempted. The CLI already reports prerequisite
problems via `df:sandbox-up --check` (exit 10, `problems[]` with `code` `prerequisites-missing` /
`prerequisites-mismatch` — see #582 / #585). This workstream decides whether the app detects directly or
treats the CLI's `--check` as authoritative, and renders the result in the UI.

Prerequisites to cover: **Docker, kubectl, helm, python**, and **minikube** (optional — only required for
the `minikube` cluster path).

## Why
Every later workstream (orchestration #7, UI step list #8, error handling #10) needs to know whether the
machine is ready. Detecting up front — and gating the provision button on it — prevents mid-deploy failures
and gives the operator a clear fix list.

## Scope
**In**
- A reusable prerequisite-check service yielding, per prerequisite, present/absent + detected version vs
  required constraint.
- A decision and implementation of *how* detection is performed: delegate to `df:sandbox-up --check`
  (recommended) or probe directly.
- A UI surface showing the readiness state and actionable remediation per missing/wrong-version item.
- A gate: the provision action is disabled (or warns + blocks) while prerequisites are unsatisfied.
- The detected state exposed for #8 (UI step list) and #10 (error handling).

**Out**
- Actually running the provision (#7).
- Binary acquisition (#5) — distinct workstream; prereq detection reports tool presence, not binary download.
- Cluster creation (#6).

## Approach
1. **Prefer delegating to the CLI's `--check`.** The CLI already encodes the version constraints and
   refuse-early behaviour (#603). Calling `df:sandbox-up --check` and consuming the structured `problems[]`
   (with `code`) avoids duplicating constraint logic and stays in sync with agent-eve. Recommended; direct
   probing of each tool is only a fallback if `--check` cannot run pre-install.
2. **Model the result** as `PrerequisiteStatus { name, required, present, detectedVersion, requiredConstraint,
   code }`, produced via the `Orchestration`/`CliRunner` layer (already scaffolded in PR #13) so the app never
   parses prose — it reads the NDJSON / `problems[]`.
3. **Gate the UI** on aggregate readiness before enabling provisioning.
4. **Unit-test** the parsing/mapping (inject a fake CLI runner returning known `--check` output) so the
   `code` → UI-message mapping is verifiable without a real cluster.

## Tasks (acceptance criteria map to #4 AC1–AC6)
- **T1 — Check service (AC1, AC5).** A `PrerequisiteChecker` (in the Orchestration layer) invokes
  `df:sandbox-up --check` via the existing `CliRunner`, captures the structured result, and exposes
  `PrerequisiteStatus[]`. No prose parsing.
- **T2 — Per-item state (AC2).** For Docker/kubectl/helm/python/minikube, surface present/absent + detected
  version vs constraint.
- **T3 — Remediation messaging (AC3).** Missing/wrong-version items carry a clear, actionable message
  (install step), mirroring #603 refuse-early.
- **T4 — UI gate (AC4).** Provision action disabled/warned while any required prerequisite is unsatisfied;
  UI shows the readiness list.
- **T5 — Decision recorded (AC5).** Document the delegate-to-CLI vs direct-probe decision + rationale in
  code/README.
- **T6 — Consumers wired (AC6).** Expose the status for #8 (step list) and #10 (error handling) to consume.

## Verification
- Unit tests: inject a fake `CliRunner` returning known `--check` NDJSON; assert correct `PrerequisiteStatus[]`
  and `code` → message mapping.
- Manual: on a machine missing Docker, the app shows Docker unsatisfied with an install pointer and disables
  provision.
- `dotnet test` green in CI (`build.yml`).

## References
- Issue #4 (ACs AC1–AC6); epic #1; depends on #2.
- Consumed by #8, #10.
- CLI contract: agent-eve #582 ("For the Windows program"), #585, #603; version coupling #582 T1.
- Orchestration scaffolding already present (PR #13: `CliRunner.cs`, `SandboxRunRequest`).
