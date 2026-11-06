# Prerequisites

The installer verifies the headless CLI's required binaries against the committed manifest
[`src/DarkFactoryInstaller/resources/artifacts.lock.json`](../src/DarkFactoryInstaller/resources/artifacts.lock.json)
(see [ADR-0003](adr/0003-binaries-verified-not-downloaded.md)). The doc-drift guard
(`tests/DarkFactoryInstaller.Tests/DocDriftGuards.cs`, G4) fails CI if this list drifts from the
manifest.

Binaries are **verified, not downloaded**: the program runs each entry's `probe` command, extracts the
version, and checks it against the constraint below (mirroring #603 refuse-early).

| Binary | Version constraint | Required |
| --- | --- | --- |
| docker | >=20.10.0 | required |
| kubectl | >=1.28.0 | required |
| helm | >=3.14.0 | required |
| python | >=3.10.0 | required |
| minikube | >=1.30.0 | optional (minikube cluster mode) |
