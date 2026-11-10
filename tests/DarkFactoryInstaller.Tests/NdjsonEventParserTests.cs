using System.Collections.Generic;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class NdjsonEventParserTests
{
    [Fact]
    public void ParsesValidEvent()
    {
        var line = @"{""ts"":""2026-01-01T00:00:00Z"",""step"":""check"",""phase"":""start"",""message"":""x""}";
        var ev = new NdjsonEventParser().ParseLine(line)!;
        Assert.Equal(ProvisionStep.Check, ev.Step);
        Assert.Equal(ProvisionPhase.Start, ev.Phase);
        Assert.Equal("x", ev.Message);
    }

    [Fact]
    public void MalformedJsonReturnsNullAndLogs() // AC1
    {
        var logged = new List<string>();
        var ev = new NdjsonEventParser(logged.Add).ParseLine("{not json");
        Assert.Null(ev);
        Assert.Single(logged);
    }

    [Fact]
    public void DoneEventCarriesExitCodeAndProblems()
    {
        var line = @"{""step"":""done"",""phase"":""failure"",""message"":""refused"",""exitCode"":10,""problems"":[{""code"":""prerequisites-missing"",""subject"":""docker"",""fix"":""install""}]}";
        var ev = new NdjsonEventParser().ParseLine(line)!;
        Assert.Equal(ProvisionStep.Done, ev.Step);
        Assert.Equal(10, ev.ExitCode);
        var p = Assert.Single(ev.Problems);
        Assert.Equal(ProvisionProblemCode.PrerequisitesMissing, p.Code);
        Assert.Equal("docker", p.Subject);
    }

    [Fact]
    public void SkipPhaseHandled() // AC2
    {
        var ev = new NdjsonEventParser().ParseLine(@"{""step"":""vendor"",""phase"":""skip"",""message"":""cached""}")!;
        Assert.Equal(ProvisionPhase.Skip, ev.Phase);
    }

    [Fact]
    public void UnknownStepIsMalformed() // AC6 (tolerates documented shape only)
    {
        var logged = new List<string>();
        Assert.Null(new NdjsonEventParser(logged.Add).ParseLine(@"{""step"":""bogus"",""phase"":""start"",""message"":""x""}"));
        Assert.Single(logged);
    }

    [Fact]
    public void ToleratesExtraFields() // AC6
    {
        var ev = new NdjsonEventParser().ParseLine(@"{""step"":""deploy"",""phase"":""success"",""message"":""ok"",""extra"":123}")!;
        Assert.Equal(ProvisionStep.Deploy, ev.Step);
    }
}