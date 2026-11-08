using System.Text.Json;
namespace Thermalith.App.Services;

public sealed class LabelCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Collection";
    public string? ParentId { get; set; }
    public List<string> Labels { get; set; } = [];
}
public sealed class CollectionLabel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public long CachedWriteTime { get; set; }
    public int ThumbnailVersion { get; set; }
}
public sealed class CollectionCatalog
{
    public List<LabelCollection> Collections { get; set; } = [];
    public List<CollectionLabel> Labels { get; set; } = [];
}
/// <summary>References only: collection operations never move or delete design files.</summary>
public sealed class CollectionStore
{
    private readonly string _directory;
    public CollectionCatalog Catalog { get; private set; } = new();
    public CollectionStore(string? directory = null)
    {
        _directory = directory ?? System.IO.Path.Combine(PlatformDirectories.AppData(), "collections");
        Directory.CreateDirectory(_directory);
        var path = System.IO.Path.Combine(_directory, "collections.json");
        if (File.Exists(path)) Catalog = JsonSerializer.Deserialize<CollectionCatalog>(File.ReadAllText(path)) ?? new();
    }
    public void Save()
    {
        var path = System.IO.Path.Combine(_directory, "collections.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Catalog, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    public LabelCollection Create(string name, string? parent)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter a collection name.");
        if (parent is not null && !Catalog.Collections.Any(c => c.Id == parent)) throw new ArgumentException("Parent collection not found.");
        var item = new LabelCollection { Name = name.Trim(), ParentId = parent };
        Catalog.Collections.Add(item); Save(); return item;
    }
    public void Move(string id, string? parentId, string? beforeId = null)
    {
        var item = Catalog.Collections.Single(c => c.Id == id);
        var seen = new HashSet<string>(); var ancestor = parentId;
        while (ancestor is not null)
        {
            if (ancestor == id || !seen.Add(ancestor)) throw new InvalidOperationException("A collection cannot be moved inside itself or a subcollection.");
            ancestor = Catalog.Collections.Single(c => c.Id == ancestor).ParentId;
        }
        if (beforeId == id) return;
        if (beforeId is not null && !Catalog.Collections.Any(c => c.Id == beforeId && c.ParentId == parentId)) throw new InvalidOperationException("Invalid drop position.");
        Catalog.Collections.Remove(item); item.ParentId = parentId;
        var index = beforeId is null ? -1 : Catalog.Collections.FindIndex(c => c.Id == beforeId);
        if (index < 0) Catalog.Collections.Add(item); else Catalog.Collections.Insert(index,item);
        Save();
    }

    public CollectionLabel Add(string collectionId, string path)
    {
        var collection = Catalog.Collections.Single(c => c.Id == collectionId);
        path = System.IO.Path.GetFullPath(path);
        var label = Catalog.Labels.FirstOrDefault(l => string.Equals(l.Path, path, StringComparison.OrdinalIgnoreCase));
        if (label is null) { label = new CollectionLabel { Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) }; Catalog.Labels.Add(label); }
        if (!collection.Labels.Contains(label.Id)) collection.Labels.Add(label.Id);
        Save(); return label;
    }
    public void Remove(string collectionId, string labelId, bool all)
    {
        foreach (var c in Catalog.Collections.Where(c => all || c.Id == collectionId)) c.Labels.Remove(labelId);
        // Keep the cached artwork until explicitly unused, never delete a design file.
        if (!Catalog.Collections.Any(c => c.Labels.Contains(labelId)))
        {
            Catalog.Labels.RemoveAll(l => l.Id == labelId);
            var cache = ThumbnailPath(labelId); if (File.Exists(cache)) File.Delete(cache);
        }
        Save();
    }
    public IEnumerable<CollectionLabel> Members(string? collectionId)
    {
        var ids = new HashSet<string>();
        void Visit(string id) { if (!ids.Add(id)) return; foreach (var c in Catalog.Collections.Where(c => c.ParentId == id)) Visit(c.Id); }
        if (collectionId is not null) Visit(collectionId);
        var labels = Catalog.Collections.Where(c => collectionId is null || ids.Contains(c.Id)).SelectMany(c => c.Labels).ToHashSet();
        return Catalog.Labels.Where(l => labels.Contains(l.Id));
    }
    public string ThumbnailPath(string id) => System.IO.Path.Combine(_directory, id + ".png");
    public void Cache(CollectionLabel label, Action<Stream> render)
    {
        var path = ThumbnailPath(label.Id);
        using (var stream = File.Create(path + ".tmp")) render(stream);
        File.Move(path + ".tmp", path, true);
        label.CachedWriteTime = File.GetLastWriteTimeUtc(label.Path).Ticks;
        label.ThumbnailVersion = 1;
        Save();
    }
}
