using System.Text.Json;

namespace Thermalith.Core.Serialization;

public sealed record PrinterOffsets(int X = 0, int Y = 0);

/// <summary>Local calibration keyed by printer serial, independent of labels and USB port names.</summary>
public sealed class PrinterOffsetStore(string path)
{
    private Dictionary<string, PrinterOffsets> Read() => File.Exists(path)
        ? JsonSerializer.Deserialize<Dictionary<string, PrinterOffsets>>(File.ReadAllText(path)) ?? new()
        : new();

    public PrinterOffsets Load(string? serial)
    {
        if (string.IsNullOrWhiteSpace(serial)) return new();
        return Read().GetValueOrDefault(serial.Trim()) ?? new();
    }

    public void Save(string serial, PrinterOffsets offsets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        var entries = Read();
        entries[serial.Trim()] = offsets;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
