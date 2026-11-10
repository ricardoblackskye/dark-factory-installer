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
    public void EveryCodeMapsToNonEmptyMessage() // AC4
    {
        Assert.False(string.IsNullOrWhiteSpace(ProblemMessageMapper.ToMessage(Provisio...[truncated]