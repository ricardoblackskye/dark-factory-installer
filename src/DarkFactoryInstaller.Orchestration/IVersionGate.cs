using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Checks the CLI version is supported before provisioning (issue #7, AC4).</summary>
public interface IVersionGate
{
    Task<VersionGateResult> CheckAsync(CancellationToken cancellationToken = default);
}
