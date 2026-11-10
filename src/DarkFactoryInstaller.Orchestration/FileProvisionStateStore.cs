using System;
using System.IO;
using System.Text.Json;
using DarkFactoryInstaller.Domain;

namespace DarkFactoryInstaller.Orchestration;

/// <summary>
/// File-backed <see cref="IProvisionStateStore"/>. Stores one JSON file per target under an isolated
/// base dir. A missing or malformed file is treated as "never run" and never blocks a provision
/// (issue #9, AC4/AC5). Writes are atomic (temp file + replace).
/// </summary>
public sealed class FileProvisionStateStore : IProvisionStateStore
{
    private static readonly string DefaultBase =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DarkFactoryInstaller", "state");

    private readonly string _baseDir;

    public FileProvisionStateStore(string? baseDir = null) => _baseDir = baseDir ?? DefaultBase;

    private string FileFor(string targetId) =>
        Path.Combine(_baseDir, targetId.Replace('\\', '_').Replace('/', '_').Replace(':', '_') + ".json");

    public ProvisionState Load(string targetId)
    {
        var file = FileFor(targetId);
        if (!File.Exists(file)) return new ProvisionState { TargetId = targetId };
        try
        {
            var text = File.ReadAllText(file);
            var state = JsonSerializer.Deserialize<ProvisionState>(text);
            return state is null ? new ProvisionState { TargetId = targetId } : state with { TargetId = targetId };
        }
        catch (Exception) // malformed -> treat as never run, never block
        {
            return new ProvisionState { TargetId = targetId };
        }
    }

    public void Save(ProvisionState state)
    {
        var file = FileFor(state.TargetId);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, file, overwrite: true);
    }

    public void Clear(string targetId)
    {
        var file = FileFor(targetId);
        if (File.Exists(file)) File.Delete(file);
    }
}