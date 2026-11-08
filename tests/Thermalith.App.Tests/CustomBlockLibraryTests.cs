using Thermalith.App.Services;
using Thermalith.App.ViewModels;
using Thermalith.App.ViewModels.Editors;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;
namespace Thermalith.App.Tests;
public class CustomBlockLibraryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(),"Thermalith-block-tests-" + Guid.NewGuid().ToString("N"));
    private BlockLibrary Library() => new(_root);
    private static LabelPackage Source(string value = "DEFAULT") => new() { Manifest = new Manifest { Id = "source" },Document = new LabelDocument {
        Metadata = new LabelMetadata { Name = "Header" },Canvas = new Canvas { WidthMm = 40,HeightMm = 20 },
        Parameters = [new LabelParameter { Name = "Serial",Prefix = value }],Elements = [new TextElement { Id = "text",X = 2,Y = 3,W = 20,H = 5,Props = new TextProps { Content = "{Serial}" } }] } };
    [Fact] public void Defaults_import_once_and_new_parameter_names_append_without_overwriting_host_values()
    {
        var library = Library(); var block = library.Create(Source()); var editor = new EditorViewModel(library);
        editor.NewDocument("Host"); editor.SetParameters([new LabelParameter { Name = "Own",Prefix = "Own" }]); editor.InsertBlock(block.Id);
        Assert.Equal(new[] { "Own","Serial" },editor.Parameters.Select(p => p.Name));
        editor.SetParameters(editor.Parameters.Select(p => p.Name == "Serial" ? p with { Prefix = "HOST" } : p).ToList());
        var changed = Source("CHANGED") with { Document = Source("CHANGED").Document with { Parameters = [new LabelParameter { Name = "Serial",Prefix = "CHANGED" },new LabelParameter { Name = "Extra",Prefix = "NEW" }] } };
        library.Save(block.Id,changed); editor.RefreshLinkedBlocks();
        Assert.Equal("HOST",editor.Parameters.Single(p => p.Name == "Serial").Prefix);
        Assert.Equal("NEW",editor.Parameters.Single(p => p.Name == "Extra").Prefix);
        var canvas = editor.CurrentCanvas(); Assert.Equal(50,canvas.WidthMm); Assert.Equal(30,canvas.HeightMm);
        var path = Path.Combine(_root,"host.nlbl"); editor.SaveTo(path);
        var reopened = new EditorViewModel(library); reopened.LoadPackage(LabelPackageIo.Load(path),path);
        Assert.Equal("HOST",reopened.Parameters.Single(p => p.Name == "Serial").Prefix);
        Assert.Contains(path,library.References(block.Id));
    }
    [Fact] public void Folder_relocation_preserves_stable_ids_and_rejects_dependency_cycles()
    {
        var library = Library(); var a = library.Create(Source());
        var b = library.Create(Source() with { Document = Source().Document with { Elements = [new BlockElement { Id = "a",BlockId = a.Id,W = 40,H = 20 }] } });
        var bad = library.Load(a.Id) with { Document = library.Load(a.Id).Document with { Elements = [new BlockElement { Id = "b",BlockId = b.Id,W = 40,H = 20 }] } };
        Assert.Throws<InvalidOperationException>(() => library.Save(a.Id,bad));
        var previous = library.PathFor(a.Id); var folder = Path.Combine(_root,"relocated"); library.ChangeFolder(folder);
        Assert.False(File.Exists(previous)); Assert.True(File.Exists(library.PathFor(a.Id)));
        Assert.Equal(a.Id,Library().Load(a.Id).Document.CustomBlockId); Assert.NotEmpty(library.References(a.Id));
    }
    [Fact] public void Block_moves_as_one_element_and_source_opens_read_only()
    {
        var library = Library(); var block = library.Create(Source()); var editor = new EditorViewModel(library); editor.NewDocument("Host"); editor.InsertBlock(block.Id);
        var inspector = Assert.IsType<BlockEditorViewModel>(editor.SelectedEditor); inspector.X = 5; inspector.Y = 6;
        // Full in-place rendering is also checked by the isolated Windows runtime smoke harness.
        Assert.Equal(5,editor.ExportPackage().Document.Elements[0].X);
        Assert.False(editor.IsEditingBlock);
        var source = new EditorViewModel(library); source.LoadPackage(library.Load(block.Id),library.PathFor(block.Id));
        Assert.True(source.IsBlockReadOnly); Assert.Throws<InvalidOperationException>(() => source.AddElement("text")); source.BlockSourceUnlocked = true; Assert.False(source.IsBlockReadOnly);
    }
    [Fact] public void Newly_added_block_parameters_are_saved_once_then_owned_by_the_design()
    {
        var library = Library(); var block = library.Create(Source()); var editor = new EditorViewModel(library); editor.NewDocument("Host"); editor.InsertBlock(block.Id);
        var path = Path.Combine(_root,"host.nlbl"); editor.SaveTo(path); Assert.False(editor.Dirty);
        var source = library.Load(block.Id);
        library.Save(block.Id,source with { Document = source.Document with { Parameters = [.. source.Document.Parameters!,new LabelParameter { Name = "Extra",Prefix = "FIRST" }] } });
        editor.RefreshLinkedBlocks(); Assert.True(editor.Dirty); editor.SaveTo(path);
        source = library.Load(block.Id);
        library.Save(block.Id,source with { Document = source.Document with { Parameters = source.Document.Parameters!.Select(p => p.Name == "Extra" ? p with { Prefix = "CHANGED DEFAULT" } : p).ToList() } });
        editor.RefreshLinkedBlocks(); Assert.False(editor.Dirty);
        Assert.Equal("FIRST",editor.Parameters.Single(p => p.Name == "Extra").Prefix);
    }
    [Fact] public void Master_parameter_rename_preserves_child_values_and_old_token_references()
    {
        var library = Library(); var block = library.Create(Source()); var editor = new EditorViewModel(library);
        editor.NewDocument("Host"); editor.InsertBlock(block.Id);
        editor.SetParameters(editor.Parameters.Select(p => p with { Prefix = "CHILD VALUE" }).ToList());
        var hostPath = Path.Combine(_root,"renamed-host.nlbl"); editor.SaveTo(hostPath);
        var source = library.Load(block.Id);
        var old = Assert.Single(source.Document.Parameters!);
        library.Save(block.Id,source with { Document = source.Document with { Parameters = [old.WithName("ProductSerial")] } });
        editor.RefreshLinkedBlocks();
        var inherited = Assert.Single(editor.Parameters);
        Assert.Equal("ProductSerial",inherited.Name); Assert.Equal("CHILD VALUE",inherited.Prefix);
        Assert.Contains("ProductSerial",editor.InheritedParameterNames);
        var resolved = Thermalith.Core.Rendering.LabelResolver.Resolve(editor.ExportPackage().Document,new Thermalith.Core.Rendering.ResolveContext { BlockProvider = library.Load });
        var nested = Assert.IsType<Thermalith.Core.Rendering.ResolvedBlock>(Assert.Single(resolved.Elements));
        Assert.Equal("CHILD VALUE",Assert.IsType<Thermalith.Core.Rendering.ResolvedText>(Assert.Single(nested.Elements)).Text);
        Assert.Throws<InvalidOperationException>(() => editor.SetParameters([inherited.WithName("IllegalChildRename")]));
        Assert.Throws<InvalidOperationException>(() => editor.SetParameters([]));
        editor.SetParameters([inherited with { Prefix = "CHANGED BY CHILD" }]); editor.SaveTo(hostPath);
        Assert.Equal("DEFAULT",library.Load(block.Id).Document.Parameters![0].Prefix);
        var reopened = new EditorViewModel(library); reopened.LoadPackage(LabelPackageIo.Load(hostPath),hostPath);
        Assert.Equal("CHANGED BY CHILD",Assert.Single(reopened.Parameters).Prefix);
    }
    [Fact] public void Rename_collision_keeps_both_design_values_and_binds_the_block_by_identity()
    {
        var library = Library(); var block = library.Create(Source()); var editor = new EditorViewModel(library);
        editor.NewDocument("Host"); editor.SetParameters([new LabelParameter { Name = "NewName",Prefix = "UNRELATED" }]); editor.InsertBlock(block.Id);
        editor.SetParameters(editor.Parameters.Select(p => p.Name == "Serial" ? p with { Prefix = "INHERITED" } : p).ToList());
        var source = library.Load(block.Id); library.Save(block.Id,source with { Document = source.Document with { Parameters = [source.Document.Parameters![0].WithName("NewName")] } });
        editor.RefreshLinkedBlocks();
        Assert.Equal("UNRELATED",editor.Parameters.Single(p => p.Name == "NewName").Prefix);
        Assert.Equal("INHERITED",editor.Parameters.Single(p => p.Name == "NewName_2").Prefix);
        var resolved = Thermalith.Core.Rendering.LabelResolver.Resolve(editor.ExportPackage().Document,new Thermalith.Core.Rendering.ResolveContext { BlockProvider = library.Load });
        var nested = Assert.IsType<Thermalith.Core.Rendering.ResolvedBlock>(Assert.Single(resolved.Elements));
        Assert.Equal("INHERITED",Assert.IsType<Thermalith.Core.Rendering.ResolvedText>(Assert.Single(nested.Elements)).Text);
    }
    [Fact] public void Nested_serial_QR_and_table_use_child_values_and_counter_survives_master_rename()
    {
        var library = Library(); var source = Source() with { Document = Source().Document with {
            Parameters = [new LabelParameter { Name = "pn",Prefix = "DEFAULT" },new LabelParameter { Name = "year",Prefix = "2026" },new LabelParameter { Name = "sn",Prefix = "ESE-{pn}-{year}-",IncrementEnabled = true,Start = 1 }],
            Elements = [new QrElement { Id = "qr",W = 10,H = 10,Props = new QrProps { Value = "{sn}" } },new TableElement { Id = "table",X = 12,W = 20,H = 6,Props = new TableProps { Rows = 1,Cols = 1,Cells = [[new TableCell { Content = "{pn}" }]] } }] } };
        var block = library.Create(source); var editor = new EditorViewModel(library); editor.NewDocument("Host");
        editor.SetParameters([new LabelParameter { Name = "pn",Prefix = "CHILD" }]); editor.InsertBlock(block.Id);
        var resolved = Thermalith.Core.Rendering.LabelResolver.Resolve(editor.ExportPackage().Document,new Thermalith.Core.Rendering.ResolveContext { BlockProvider = library.Load });
        var nested = Assert.IsType<Thermalith.Core.Rendering.ResolvedBlock>(Assert.Single(resolved.Elements));
        Assert.Equal("ESE-CHILD-2026-1",Assert.IsType<Thermalith.Core.Rendering.ResolvedQr>(nested.Elements[0]).Value);
        Assert.Equal("CHILD",Assert.IsType<Thermalith.Core.Rendering.ResolvedTable>(nested.Elements[1]).Cells[0][0].Text);
        editor.BeginParameterPrintRun(1); editor.CompleteParameterLabel(); editor.EndParameterPrintRun();
        source = library.Load(block.Id); library.Save(block.Id,source with { Document = source.Document with { Parameters = source.Document.Parameters!.Select(p => p.Name == "sn" ? p.WithName("serial_number") : p).ToList() } });
        editor.RefreshLinkedBlocks(); Assert.Equal(2,editor.Parameters.Single(p => p.Name == "serial_number").Start);
        Assert.Equal("DEFAULT",library.Load(block.Id).Document.Parameters!.Single(p => p.Name == "pn").Prefix);
    }
    [Fact] public void Renamed_inherited_counter_remembers_prints_even_before_child_design_is_saved_again()
    {
        var library = Library(); var package = Source() with { Document = Source().Document with { Parameters = [new LabelParameter { Name = "Serial",Prefix = "SN-",IncrementEnabled = true,Start = 7 }] } };
        var block = library.Create(package); var editor = new EditorViewModel(library); editor.NewDocument("Host"); editor.InsertBlock(block.Id);
        var path = Path.Combine(_root,"counter-host.nlbl"); editor.SaveTo(path);
        var source = library.Load(block.Id); library.Save(block.Id,source with { Document = source.Document with { Parameters = [source.Document.Parameters![0].WithName("ProductSerial")] } });
        editor.RefreshLinkedBlocks(); editor.BeginParameterPrintRun(1); editor.CompleteParameterLabel(); editor.EndParameterPrintRun();
        // Re-open the pre-rename file without saving the automatic metadata update.
        var reopened = new EditorViewModel(library); reopened.LoadPackage(LabelPackageIo.Load(path),path);
        Assert.Equal(8,Assert.Single(reopened.Parameters).Start);
        Assert.Equal("ProductSerial",Assert.Single(reopened.Parameters).Name);
    }
    [Fact] public void Multiple_blocks_sharing_a_parameter_do_not_toggle_names_back_after_a_rename()
    {
        var library = Library(); var a = library.Create(Source()); var b = library.Create(library.Load(a.Id));
        var editor = new EditorViewModel(library); editor.NewDocument("Host"); editor.InsertBlock(a.Id); editor.InsertBlock(b.Id);
        var source = library.Load(b.Id); library.Save(b.Id,source with { Document = source.Document with { Parameters = [source.Document.Parameters![0].WithName("NewName")] } });
        editor.RefreshLinkedBlocks(); Assert.Equal("NewName",Assert.Single(editor.Parameters).Name);
        var path = Path.Combine(_root,"shared-host.nlbl"); editor.SaveTo(path);
        editor.RefreshLinkedBlocks(); Assert.False(editor.Dirty); Assert.Equal("NewName",Assert.Single(editor.Parameters).Name);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
}
