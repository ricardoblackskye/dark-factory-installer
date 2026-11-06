namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// Receives the two output channels of a provision run separately (issue #7, AC3): stdout carries
/// NDJSON events, stderr carries prose. The UI (#8) and error handler (#10) consume these.
/// </summary>
public interface IOutputSink
{
    /// <summary>One NDJSON event line from stdout.</summary>
    void OnStdout(string line);

    /// <summary>One prose line from stderr.</summary>
    void OnStderr(string line);
}
