using Thermalith.App.Services;
using Xunit;
namespace Thermalith.App.Tests;
public class CollectionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Thermalith-collections-" + Guid.NewGuid().ToString("N"));
    private CollectionStore Store() => new(Path.Combine(_root,"catalog"));
    private string Design()
    {
        Directory.CreateDirectory(_root); var path = Path.Combine(_root,"design.nlbl"); File.WriteAllText(path,"design"); return path;
    }
    [Fact] public void Hierarchy_references_one_file_and_counts_unique_descendants()
    {
        var store = Store(); var parent = store.Create("Project",null); var child = store.Create("Panels",parent.Id);
        var path = Design(); var label = store.Add(parent.Id,path); var same = store.Add(child.Id,path);
        Assert.Equal(label.Id,same.Id); Assert.Single(store.Catalog.Labels);
        Assert.Single(store.Members(parent.Id)); Assert.Single(store.Members(child.Id)); Assert.Single(store.Members(null));
        var loaded = Store(); Assert.Equal(parent.Id,loaded.Catalog.Collections.Single(c => c.Id == child.Id).ParentId);
        Assert.Single(loaded.Catalog.Labels); Assert.Equal(path,loaded.Catalog.Labels[0].Path);
    }
    [Fact] public void Missing_file_keeps_membership_and_cached_artwork()
    {
        var store = Store(); var collection = store.Create("Labels",null); var path = Design(); var label = store.Add(collection.Id,path);
        store.Cache(label,s => s.Write(new byte[] { 1,2,3 })); File.Delete(path);
        var loaded = Store(); Assert.Single(loaded.Members(collection.Id));
        Assert.Equal(new byte[] { 1,2,3 },File.ReadAllBytes(loaded.ThumbnailPath(label.Id)));
    }
    [Fact] public void Removing_references_never_deletes_design_and_all_option_removes_every_membership()
    {
        var store = Store(); var a = store.Create("A",null); var b = store.Create("B",null); var path = Design();
        var label = store.Add(a.Id,path); store.Add(b.Id,path); store.Cache(label,s => s.WriteByte(1));
        store.Remove(a.Id,label.Id,false); Assert.Empty(store.Members(a.Id)); Assert.Single(store.Members(b.Id));
        Assert.True(File.Exists(store.ThumbnailPath(label.Id))); Assert.True(File.Exists(path));
        store.Add(a.Id,path); store.Remove(a.Id,label.Id,true);
        Assert.Empty(store.Catalog.Labels); Assert.Empty(store.Members(null)); Assert.True(File.Exists(path));
    }
    [Fact] public void Moving_collections_preserves_references_and_rejects_cycles()
    {
        var store = Store(); var a = store.Create("A",null); var b = store.Create("B",null); var child = store.Create("Child",a.Id);
        var path = Design(); store.Add(child.Id,path);
        Assert.Throws<InvalidOperationException>(() => store.Move(a.Id,child.Id)); Assert.Null(a.ParentId);
        store.Move(child.Id,b.Id); Assert.Empty(store.Members(a.Id)); Assert.Single(store.Members(b.Id));
        store.Move(child.Id,null,a.Id); Assert.Equal(child.Id,store.Catalog.Collections[0].Id); Assert.Null(child.ParentId);
        Assert.Single(Store().Members(null)); Assert.True(File.Exists(path));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
}
