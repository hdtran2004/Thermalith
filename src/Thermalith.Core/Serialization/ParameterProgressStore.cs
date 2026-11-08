using System.Text.Json;
using Thermalith.Core.Model;

namespace Thermalith.Core.Serialization;

/// <summary>Automatic print progress, kept separate from unsaved artwork.</summary>
public sealed class ParameterProgressStore(string path)
{
    public sealed record Entry(string Definition, long Next,string? ParameterId = null);
    private Dictionary<string, Entry> Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path)) ?? new()
        : new();
    private static string Key(string documentId, LabelParameter p) => documentId + "/" + p.Name;
    private static string Signature(LabelParameter p) => JsonSerializer.Serialize(p with { Id = null,PreviousNames = null });
    public List<LabelParameter> Resume(string documentId, IReadOnlyList<LabelParameter> definitions)
    {
        var entries = Read();
        return definitions.Select(p => p.IncrementEnabled && entries.TryGetValue(Key(documentId, p), out var entry)
            && entry.Definition == Signature(p) ? p with { Start = entry.Next } : p).ToList();
    }
    public void MigrateRename(string documentId,LabelParameter previous,LabelParameter renamed)
    {
        if (!renamed.IncrementEnabled) return;
        var entries = Read(); var candidates = new List<long>();
        if (entries.TryGetValue(Key(documentId,previous),out var legacy) && legacy.Definition == Signature(previous)) candidates.Add(legacy.Next);
        foreach (var (key,entry) in entries)
        {
            if (previous.Id is null || entry.ParameterId != previous.Id || !key.StartsWith(documentId + "/",StringComparison.Ordinal)) continue;
            var definition = JsonSerializer.Deserialize<LabelParameter>(entry.Definition);
            if (definition is not null && Signature(definition with { Name = renamed.Name }) == Signature(renamed)) candidates.Add(entry.Next);
        }
        if (candidates.Count == 0) return;
        entries[Key(documentId,renamed)] = new Entry(Signature(renamed),candidates.Max(),renamed.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp,JsonSerializer.Serialize(entries)); File.Move(temp,path,true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void SaveNext(string documentId, IReadOnlyList<LabelParameter> definitions, IReadOnlyList<LabelParameter> effective, int completed)
    {
        var entries = Read();
        for (var i = 0; i < definitions.Count; i++)
        {
            var p = definitions[i];
            if (p.IncrementEnabled) entries[Key(documentId, p)] = new Entry(Signature(p), checked(effective[i].Start + (long)completed * p.Step),p.Id);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
