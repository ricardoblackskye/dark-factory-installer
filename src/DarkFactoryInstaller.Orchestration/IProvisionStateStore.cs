using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>Persists per-target provisioning progress (issue #9, AC1/AC4/AC5).</summary>
public interface IProvisionStateStore
{
    ProvisionState Load(string targetId);
    void Save(ProvisionState state);
    void Clear(string targetId);
}