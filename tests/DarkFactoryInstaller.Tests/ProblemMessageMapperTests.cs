using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class ProblemMessageMapperTests
{
    [Theory]
    [InlineData(ProvisionProblemCode.PrerequisitesMissing)]
    [InlineData(ProvisionProblemCode.PrerequisitesMismatch)]
    [InlineData(ProvisionProblemCode.ArtifactUnresolvable)]
    [InlineData(ProvisionProblemCode.ClusterUnreachable)]
    [InlineData(ProvisionProblemCode.ProofFailed)]
    [InlineData(ProvisionProblemCode.TeardownFailed)]
    [InlineData(ProvisionProblemCode.Internal)]
    public void EveryCodeMapsToNonEmptyMessage(ProvisionProblemCode code) // AC4
    {
        Assert.False(string.IsNullOrWhiteSpace(ProblemMessageMapper.ToMessage(code)));
    }

    [Fact]
    public void MessagesAreDistinct() // mapping by code, not prose
    {
        var msgs = new[]
        {
            ProvisionProblemCode.PrerequisitesMissing,
            ProvisionProblemCode.PrerequisitesMismatch,
        }.Select(ProblemMessageMapper.ToMessage);
        Assert.Equal(2, msgs.Distinct().Count());
    }
}