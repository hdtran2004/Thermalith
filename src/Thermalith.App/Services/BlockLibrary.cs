using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
namespace Thermalith.App.Services;

public sealed class CustomBlockRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public List<string> Designs { get; set; } = [];
}
public sealed class BlockLibraryCatalog
{
    public string Folder { get; set; } = "";
    public List<CustomBlockRecord> Blocks { get; set; } = [];
}
/// <summary>Stable-ID block library; the index stays outside the relocatable block folder.</summary>
public sealed class BlockLibrary
{
    private static BlockLibrary? _shared;
    public static BlockLibrary Shared => _shared ??= new();
    private readonly string _config;
    private readonly string _baseDirectory;
    private readonly Dictionary<string,(long Stamp,LabelPackage Package)> _cache = [];
    public BlockLibraryCatalog Catalog { get; }
    public string Folder => Catalog.Folder;
    public string ThumbnailPath(string id) => Path.Combine(_baseDirectory,id + ".png");
    public BlockLibrary(string? directory = null)
    {
        _baseDirectory = directory ?? Path.Combine(PlatformDirectories.AppData(),"block-library");
        Directory.CreateDirectory(_baseDirectory); _config = Path.Combine(_baseDirectory,"library.json");
        Catalog = File.Exists(_config) ? JsonSerializer.Deserialize<BlockLibraryCatalog>(File.ReadAllText(_config)) ?? new() : new();
        if (string.IsNullOrWhiteSpace(Catalog.Folder))
        {
            Catalog.Folder = Path.Combine(_baseDirectory,"blocks");
            Directory.CreateDirectory(Catalog.Folder);
        }
    }
    private void SaveIndex()
    {
        File.WriteAllText(_config + ".tmp",JsonSerializer.Serialize(Catalog,new JsonSerializerOptions { WriteIndented = true }));
        File.Move(_config + ".tmp",_config,true);
    }
    public string PathFor(string id)
    {
        var record = Catalog.Blocks.SingleOrDefault(b => b.Id == id) ?? throw new FileNotFoundException("Custom block is not in the library: " + id);
        if (Path.GetFileName(record.FileName) != record.FileName) throw new IOException("Invalid block file name.");
        return Path.Combine(Folder,record.FileName);
    }
    public LabelPackage Load(string id)
    {
        var path = PathFor(id);
        if (!File.Exists(path)) throw new FileNotFoundException("Custom block file is missing: " + path);
        var stamp = File.GetLastWriteTimeUtc(path).Ticks;
        if (_cache.TryGetValue(id,out var entry) && entry.Stamp == stamp) return entry.Package;
        var package = LabelPackageIo.Load(path);
        if (package.Document.CustomBlockId != id) throw new IOException("Custom block identity does not match: " + path);
        package = package with { Document = package.Document with { Parameters = ParameterIdentity.Assign(package.Document.Parameters,id) } };
        _cache[id] = (stamp,package); return package;
    }
    public CustomBlockRecord Create(LabelPackage package)
    {
        var id = Guid.NewGuid().ToString("N");
        var record = new CustomBlockRecord { Id = id,Name = package.Document.Metadata.Name,FileName = id + ".nlbl" };
        var block = package with { Document = package.Document with { CustomBlockId = id }, Manifest = package.Manifest with { Id = id } };
        Catalog.Blocks.Add(record);
        try { Save(id,block); } catch { Catalog.Blocks.Remove(record); throw; }
        return record;
    }
    public void Save(string id,LabelPackage package)
    {
        var previous = File.Exists(PathFor(id)) ? Load(id).Document.Parameters ?? [] : new List<LabelParameter>();
        package = package with { Document = package.Document with { Parameters = ParameterIdentity.PreserveNames(package.Document.Parameters ?? [],previous,id) } };
        // Validate the full dependency graph before publishing an update.
        var seen = new HashSet<string>();
        void Check(LabelDocument doc)
        {
            foreach (var block in doc.Elements.OfType<BlockElement>())
            {
                if (block.BlockId == id || !seen.Add(block.BlockId))
                { if (block.BlockId == id) throw new InvalidOperationException("A custom block cannot reference itself, including through other blocks."); else continue; }
                Check(Load(block.BlockId).Document);
            }
        }
        Check(package.Document);
        var path = PathFor(id);
        if (!Directory.Exists(Folder)) throw new DirectoryNotFoundException("Custom block folder is unavailable. Choose its location through Block folder: " + Folder);
        LabelPackageIo.Save(package with { Document = package.Document with { CustomBlockId = id } },path + ".tmp");
        File.Move(path + ".tmp",path,true); _cache.Remove(id);
        Catalog.Blocks.Single(b => b.Id == id).Name = package.Document.Metadata.Name;
        SaveIndex();
    }
    public IReadOnlyList<LabelParameter> Parameters(string id)
    {
        var result = new Dictionary<string,LabelParameter>(StringComparer.Ordinal); var stack = new HashSet<string>();
        void Visit(string key)
        {
            if (!stack.Add(key)) throw new InvalidOperationException("Circular custom block reference.");
            if (stack.Count > 16) throw new InvalidOperationException("Custom blocks are nested too deeply.");
            var doc = Load(key).Document;
            foreach (var p in doc.Parameters ?? [])
            {
                var existing = result.Values.FirstOrDefault(old => old.Id == p.Id);
                if (existing is not null && existing.Name != p.Name && (p.PreviousNames ?? []).Contains(existing.Name))
                {
                    result.Remove(existing.Name); result[p.Name] = existing.WithName(p.Name);
                }
                else if (existing is null) result.TryAdd(p.Name,p);
            }
            foreach (var child in doc.Elements.OfType<BlockElement>()) Visit(child.BlockId);
            stack.Remove(key);
        }
        Visit(id); return result.Values.ToArray();
    }
    public string Signature() => string.Join(";",Catalog.Blocks.Select(b => b.Id + ":" + (File.Exists(PathFor(b.Id)) ? File.GetLastWriteTimeUtc(PathFor(b.Id)).Ticks : -1)));
    public void Register(string? path,LabelDocument document)
    {
        if (string.IsNullOrEmpty(path)) return;
        var used = new HashSet<string>();
        void Visit(LabelDocument doc)
        {
            foreach (var block in doc.Elements.OfType<BlockElement>())
                if (used.Add(block.BlockId)) { try { Visit(Load(block.BlockId).Document); } catch (IOException) { } }
        }
        Visit(document); bool changed = false;
        foreach (var record in Catalog.Blocks)
        {
            var contains = record.Designs.Contains(path,StringComparer.OrdinalIgnoreCase);
            if (used.Contains(record.Id) && !contains) { record.Designs.Add(path); changed = true; }
            if (!used.Contains(record.Id) && contains) { record.Designs.RemoveAll(p => string.Equals(p,path,StringComparison.OrdinalIgnoreCase)); changed = true; }
        }
        if (changed) SaveIndex();
    }
    public IReadOnlyList<string> References(string id)
    {
        var names = new HashSet<string>(Catalog.Blocks.Single(b => b.Id == id).Designs,StringComparer.OrdinalIgnoreCase);
        foreach (var block in Catalog.Blocks.Where(b => b.Id != id))
        {
            try { if (Load(block.Id).Document.Elements.OfType<BlockElement>().Any(e => e.BlockId == id)) names.Add("Custom block: " + block.Name); } catch (IOException) { }
        }
        return names.ToArray();
    }
    public void Delete(string id)
    {
        File.Delete(PathFor(id)); Catalog.Blocks.RemoveAll(b => b.Id == id); _cache.Remove(id); SaveIndex();
    }
    public void ChangeFolder(string folder)
    {
        folder = Path.GetFullPath(folder);
        if (string.Equals(folder,Path.GetFullPath(Folder),StringComparison.OrdinalIgnoreCase)) return;
        Directory.CreateDirectory(folder);
        var files = Catalog.Blocks.Select(b => (Source:PathFor(b.Id),Destination:Path.Combine(folder,b.FileName))).ToArray();
        foreach (var file in files)
            if (!File.Exists(file.Source) || File.Exists(file.Destination)) throw new IOException("Cannot relocate blocks: a source is missing or a destination already exists. " + file.Source);
        var moved = new List<(string Source,string Destination)>(); var previous = Folder;
        try
        {
            foreach (var file in files) { File.Move(file.Source,file.Destination); moved.Add(file); }
            Catalog.Folder = folder; SaveIndex(); _cache.Clear();
        }
        catch
        {
            Catalog.Folder = previous;
            foreach (var file in moved.AsEnumerable().Reverse()) File.Move(file.Destination,file.Source);
            throw;
        }
    }
}
