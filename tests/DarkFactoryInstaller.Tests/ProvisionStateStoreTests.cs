using System.IO;
using System.Linq;
using DarkFactoryInstaller.Domain;
using DarkFactoryInstaller.Orchestration;
using Xunit;

namespace DarkFactoryInstaller.Tests;

public class ProvisionStateStoreTests
{
    [Fact]
    public void RoundTripsCompletedStepsAndOutcome() // AC1
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-state-" + Path.GetRandomFileName());
        try
        {
            var store = new FileProvisionStateStore(dir);
            store.Save(new ProvisionState
            {
                TargetId = "ricardoblackskye/agent-eve#1",
                CompletedSteps = new() { ProvisionStep.Check, ProvisionStep.Vendor },
                Outcome = ProvisionOutcome.Succeeded,
                Timestamp = "2026-01-01T00:00:00Z",
            });
            var loaded = store.Load("ricardoblackskye/agent-eve#1");
            Assert.Equal(ProvisionOutcome.Succeeded, loaded.Outcome);
            Assert.Equal(2, loaded.CompletedSteps.Count);
            Assert.Contains(ProvisionStep.Check, loaded.CompletedSteps);
            Assert.Contains(ProvisionStep.Vendor, loaded.CompletedSteps);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void IsolatedPerTarget() // AC4
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-state-" + Path.GetRandomFileName());
        try
        {
            var store = new FileProvisionStateStore(dir);
            store.Save(new ProvisionState { TargetId = "a/b", CompletedSteps = new() { ProvisionStep.Check }, Outcome = ProvisionOutcome.Succeeded });
            store.Save(new ProvisionState { TargetId = "c/d", CompletedSteps = new() { ProvisionStep.Deploy }, Outcome = ProvisionOutcome.Failed });
            Assert.Equal(ProvisionOutcome.Succeeded, store.Load("a/b").Outcome);
            Assert.Equal(ProvisionOutcome.Failed, store.Load("c/d").Outcome);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void MalformedFileTreatedAsAbsent() // AC5
    {
        var dir = Path.Combine(Path.GetTempPath(), "df-state-" + Path.GetRandomFileName());
        try
        {
            var store = new FileProvisionStateStore(dir);
            Directory.CreateDirectory(dir); // the malformed file is written before any Save would create it
            var target = "x/y";
            File.WriteAllText(Path.Combine(dir, "x_y.json"), "{not valid json");
            var loaded = store.Load(target);
            Assert.Equal(target, loaded.TargetId);
            Assert.Empty(loaded.CompletedSteps);
            Assert.Equal(ProvisionOutcome.InProgress, loaded.Outcome);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}