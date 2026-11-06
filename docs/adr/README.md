# Architecture Decision Records

This directory holds the Dark Factory Installer's Architecture Decision Records (ADRs), following
the [agent-eve auto-documentation philosophy](https://github.com/ricardoblackskye/agent-eve) (ADR-0012 +
the #238 doc-drift-guard tests). Decisions are **hand-maintained** and kept honest by xUnit doc-drift-guard
tests that **fail CI on documentation drift** (`tests/DarkFactoryInstaller.Tests/DocDriftGuards.cs`).

**Convention:** documentation changes accompany the code they describe, in the same PR. See
[ADR-0004](0004-documentation-update-strategy.md).

## Index

| ADR | Title | Decision |
| --- | --- | --- |
| [0001](0001-framework-choice.md) | Desktop UI framework — WPF | WPF / .NET 8 for the Windows shell. |
| [0002](0002-prereq-delegates-to-cli.md) | Prerequisite detection delegates to the CLI | Delegates to `df:sandbox-up --check` (exit-code gate + stderr prose). |
| [0003](0003-binaries-verified-not-downloaded.md) | Binaries verified, not downloaded | The program carries the manifest; it never downloads binaries (#5). |
| [0004](0004-documentation-update-strategy.md) | Documentation-update strategy | Option A: hand-maintained ADRs + drift-guard tests; same-PR convention. |

## Adding a new ADR

Later workstreams should add an ADR (next free number, `000N-slug.md`) when they make an architecture
decision, link it from the table above, and update the docs in the same PR as the code.
