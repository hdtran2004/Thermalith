using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.App.Services;
using Thermalith.App.Rendering;
using Thermalith.Core.History;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;

namespace Thermalith.App.ViewModels;
public sealed partial class EditorViewModel
{
    public string? CustomBlockId => _live.CustomBlockId;
    public bool IsBlockSource => CustomBlockId is not null;
    [ObservableProperty] private bool _blockSourceUnlocked;
    public bool IsBlockReadOnly => IsBlockSource && !BlockSourceUnlocked;
    partial void OnBlockSourceUnlockedChanged(bool value) { OnPropertyChanged(nameof(IsBlockReadOnly)); RaiseState(); }
    public bool IsEditingBlock => _blockEdit is not null;
    public bool HasSelectedBlock => SelectedEditor is not null && _live.Elements.Any(e => e.Id == SelectedEditor.Id && e is BlockElement);
    private record BlockSession(LabelPackage Parent,string? Path,bool Dirty,SnapshotHistory History,BlockElement Instance,LabelPackage Source,string Backdrop,List<LabelParameter> Parameters);
    private BlockSession? _blockEdit;
    private List<LabelParameter>? _blockDefaults;
    public IReadOnlyList<LabelParameter> BlockDefaultParameters => _blockDefaults ?? [];
    public void SetBlockDefaults(List<LabelParameter> defaults)
    {
        if (!IsEditingBlock) throw new InvalidOperationException("No active block edit.");
        Thermalith.Core.Tokens.ParameterResolver.ResolveAll(defaults);
        _blockDefaults = ParameterIdentity.PreserveNames(defaults,_blockDefaults ?? [],_blockEdit!.Instance.BlockId);
        var parameters = (_live.Parameters ?? []).ToList();
        foreach (var p in _blockDefaults)
        {
            var target = p.Id is not null && _blockEdit!.Instance.ParameterBindings.TryGetValue(p.Id,out var bound) ? bound : p.Id;
            var existing = parameters.FirstOrDefault(own => own.Id == target) ?? parameters.FirstOrDefault(own => own.Name == p.Name);
            if (existing is null) parameters.Add(p);
            else if (existing.Name != p.Name && !parameters.Any(other => other.Id != existing.Id && other.Name == p.Name))
                parameters[parameters.IndexOf(existing)] = existing.WithName(p.Name);
        }
        _live = _live with { Parameters = parameters }; MarkDirty(); RenderNow();
    }
    private (IReadOnlyDictionary<string,string>? Names,IReadOnlyDictionary<string,string>? Ids) BlockEditValues()
    {
        if (_blockEdit is null) return (null,null);
        var effective = _parameterProgress.Resume(_manifest.Id,_live.Parameters ?? []);
        var original = Thermalith.Core.Tokens.ParameterResolver.ResolveAll(effective);
        var names = new Dictionary<string,string>(original);
        var ids = effective.Where(p => p.Id is not null).GroupBy(p => p.Id!).ToDictionary(g => g.Key,g => original[g.First().Name]);
        var canonical = new Dictionary<string,string>();
        foreach (var p in _blockDefaults ?? [])
        {
            var target = p.Id is not null && _blockEdit.Instance.ParameterBindings.TryGetValue(p.Id,out var bound) ? bound : p.Id;
            if (target is null || !ids.TryGetValue(target,out var value)) continue;
            if (p.Id is not null) ids[p.Id] = value;
            foreach (var alias in p.PreviousNames ?? []) names[alias] = value;
            canonical[p.Name] = value;
        }
        foreach (var (name,value) in canonical) names[name] = value;
        return (names,ids);
    }
    private void RequireEditable()
    {
        if (IsBlockReadOnly) throw new InvalidOperationException("This custom block is read-only. Deliberately unlock it before editing.");
    }
    private Dictionary<string,LabelPackage>? _blockPrintSnapshot;
    private LabelPackage LoadBlock(string id) => _blockPrintSnapshot is not null && _blockPrintSnapshot.TryGetValue(id,out var frozen) ? frozen : _blocks.Load(id);
    public IReadOnlySet<string> InheritedParameterNames
    {
        get
        {
            var ids = _live.Elements.OfType<BlockElement>().SelectMany(b => b.ParameterBindings.Values).ToHashSet();
            return (_live.Parameters ?? []).Where(p => p.Id is not null && ids.Contains(p.Id)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        }
    }
    private void SyncBlockParameters()
    {
        if (_blockPrintSnapshot is not null) return;
        var parameters = ParameterIdentity.Assign(_live.Parameters,_live.CustomBlockId ?? _manifest.Id);
        var changed = false;
        var elements = _live.Elements.Select(e =>
        {
            if (e is not BlockElement block) return e;
            IReadOnlyList<LabelParameter> defaults;
            try { defaults = _blocks.Parameters(block.BlockId); }
            catch (IOException) { return e; }
            var bindings = new Dictionary<string,string>(block.ParameterBindings);
            var sourceNames = new Dictionary<string,string>(block.ParameterSourceNames);
            var addedIds = new HashSet<string>();
            var names = new List<string>();
            foreach (var sourceParameter in defaults)
            {
                var sourceId = sourceParameter.Id!;
                var existing = bindings.TryGetValue(sourceId,out var target) ? parameters.FirstOrDefault(p => p.Id == target) : null;
                existing ??= parameters.FirstOrDefault(p => p.Id == sourceId || p.Name == sourceParameter.Name || (sourceParameter.PreviousNames ?? []).Contains(p.Name));
                if (existing is null)
                {
                    existing = sourceParameter with { ProgressRevision = Guid.NewGuid().ToString("N") };
                    parameters.Add(existing); addedIds.Add(existing.Id!); changed = true;
                }
                else if (existing.Name != sourceParameter.Name && ((sourceParameter.PreviousNames ?? []).Contains(existing.Name) || sourceNames.TryGetValue(sourceId,out var previousSourceName) && previousSourceName != sourceParameter.Name))
                {
                    var name = sourceParameter.Name; var suffix = 2;
                    while (parameters.Any(p => p.Id != existing.Id && p.Name == name)) name = sourceParameter.Name + "_" + suffix++;
                    if (name != existing.Name)
                    {
                        var renamed = existing.WithName(name);
                        renamed = renamed with { PreviousNames = (renamed.PreviousNames ?? []).Concat(sourceParameter.PreviousNames ?? []).Where(alias => alias != name && !parameters.Any(other => other.Id != existing.Id && other.Name == alias)).Distinct(StringComparer.Ordinal).ToList() };
                        _parameterProgress.MigrateRename(_manifest.Id,existing,renamed);
                        parameters[parameters.IndexOf(existing)] = renamed; existing = renamed; changed = true;
                    }
                }
                bindings[sourceId] = existing.Id!; sourceNames[sourceId] = sourceParameter.Name; names.Add(existing.Name);
            }
            var rewrite = new Dictionary<string,string>();
            foreach (var p in defaults) rewrite[p.Name] = parameters.First(own => own.Id == bindings[p.Id!]).Name;
            foreach (var p in defaults) foreach (var alias in p.PreviousNames ?? []) rewrite.TryAdd(alias,rewrite[p.Name]);
            for (var i = 0; i < parameters.Count; i++) if (addedIds.Contains(parameters[i].Id!))
                parameters[i] = parameters[i] with { Prefix = Thermalith.Core.Tokens.TokenResolver.RewriteNames(parameters[i].Prefix,rewrite),Suffix = Thermalith.Core.Tokens.TokenResolver.RewriteNames(parameters[i].Suffix,rewrite) };
            var source = LoadBlock(block.BlockId).Document;
            return block with { ParameterNames = names,ParameterBindings = bindings,ParameterSourceNames = sourceNames,W = source.Canvas.WidthMm,H = source.Canvas.HeightMm };
        }).ToList();
        if (_live.Elements.OfType<BlockElement>().Any())
        {
            _live = _live with { Parameters = parameters,Elements = elements };
            if (changed) MarkDirty();
        }
    }
    public LabelPackage ExportPackage()
    {
        FlushGesture(); SyncBlockParameters();
        return new LabelPackage { Document = _live,Manifest = _manifest,Assets = _assets };
    }
    public void InsertBlock(string id)
    {
        if (IsBlockReadOnly) throw new InvalidOperationException("Unlock the custom block before editing.");
        if (IsEditingBlock) throw new InvalidOperationException("Finish the current block edit before inserting another block.");
        if (id == CustomBlockId) throw new InvalidOperationException("A block cannot reference itself.");
        FlushGesture(); var package = _blocks.Load(id);
        var block = new BlockElement { Id = DocumentFactory.NewId(),BlockId = id, Name = package.Document.Metadata.Name,
            X = 0,Y = 0,W = package.Document.Canvas.WidthMm,H = package.Document.Canvas.HeightMm };
        _live = _live with { Elements = _live.Elements.Append(block).ToList() };
        try { SyncBlockParameters(); }
        catch { _live = _live with { Elements = _live.Elements.Where(e => e.Id != block.Id).ToList() }; throw; }
        _history.Commit(_live); RebuildLayers(); SelectedLayer = Layers.FirstOrDefault(l => l.Id == block.Id);
        MarkDirty(); RenderNow(); RaiseState();
    }
    public void RefreshLinkedBlocks()
    {
        if (IsEditingBlock) return;
        if (CustomBlockId is { } sourceId && IsBlockReadOnly && !Dirty)
        {
            try { var package = _blocks.Load(sourceId); LoadInternal(package.Document,package.Manifest,_blocks.PathFor(sourceId),package.Assets); }
            catch (Exception ex) { StatusText = "Block refresh failed: " + ex.Message; }
            return;
        }
        if (!_live.Elements.OfType<BlockElement>().Any()) return;
        try
        {
            var selected = _selectedIds.ToArray(); var primary = SelectedEditor?.Id;
            SyncBlockParameters(); RenderNow(); RebuildLayers(); SetSelection(selected,primary); RaiseState();
        }
        catch (Exception ex) { StatusText = "Block refresh failed: " + ex.Message; }
    }
    public void BeginBlockEdit()
    {
        if (IsEditingBlock) throw new InvalidOperationException("A block is already unlocked for editing.");
        if (IsBlockReadOnly) throw new InvalidOperationException("Unlock this custom block first.");
        var instance = _live.Elements.OfType<BlockElement>().Single(e => e.Id == SelectedEditor!.Id);
        FlushGesture(); SyncBlockParameters(); var source = LoadBlock(instance.BlockId);
        if (Math.Abs(instance.Rotation) > 0.001) throw new InvalidOperationException("A rotated block must be edited through the Custom blocks library.");
        var backdropId = DocumentFactory.NewId();
        using var preview = PreviewImage.FromGray(_renderer.RenderGray(_live with { Elements = _live.Elements.Where(e => e.Id != instance.Id).ToList() },
            new ResolveContext { Assets = _assets, BlockProvider = LoadBlock },new RenderOptions { ApplyOrientation = false }));
        using var bytes = new MemoryStream(); preview.Save(bytes);
        var parent = ExportPackage(); var effective = Parameters.ToList();
        _blockDefaults = (source.Document.Parameters ?? []).ToList();
        _blockEdit = new BlockSession(parent,FilePath,Dirty,_history,instance,source,backdropId,effective);
        var elements = source.Document.Elements.Select(e => e with { X = e.X + instance.X,Y = e.Y + instance.Y }).ToList();
        elements.Insert(0,new ImageElement { Id = backdropId,Name = "Reference design (read-only)",Locked = true,W = _live.Canvas.WidthMm,H = _live.Canvas.HeightMm,
            Props = new ImageProps { AssetId = backdropId,Fit = "stretch",Dither = "none" } });
        var assets = new Dictionary<string,byte[]>(source.Assets) { [backdropId] = bytes.ToArray() };
        LoadInternal(parent.Document with { CustomBlockId = null,Elements = elements,Parameters = effective },parent.Manifest,FilePath,assets);
        _canvasEditor.ReadOnlyContext = true;
        OnPropertyChanged(nameof(IsEditingBlock)); RaiseState();
    }
    public void SaveBlockEdit()
    {
        var session = _blockEdit ?? throw new InvalidOperationException("No active block edit.");
        FlushGesture();
        var elements = _live.Elements.Where(e => e.Id != session.Backdrop).Select(e => e with { X = e.X - session.Instance.X,Y = e.Y - session.Instance.Y }).ToList();
        var parameters = _blockDefaults ?? session.Source.Document.Parameters ?? [];
        var updated = session.Source with { Document = session.Source.Document with { Elements = elements,Parameters = parameters },Assets = _assets.Where(kv => kv.Key != session.Backdrop).ToDictionary(kv => kv.Key,kv => kv.Value) };
        _blocks.Save(session.Instance.BlockId,updated); FinishBlockEdit();
    }
    public void CancelBlockEdit() { if (_blockEdit is not null) FinishBlockEdit(); }
    private void FinishBlockEdit()
    {
        var session = _blockEdit!; _blockEdit = null; _blockDefaults = null;
        LoadInternal(session.Parent.Document,session.Parent.Manifest,session.Path,session.Parent.Assets);
        _history = session.History; Dirty = session.Dirty || Dirty;
        OnPropertyChanged(nameof(IsEditingBlock)); RefreshLinkedBlocks(); RaiseState();
    }
    public void FreezeBlockSources()
    {
        SyncBlockParameters(); var result = new Dictionary<string,LabelPackage>();
        void Visit(LabelDocument doc)
        {
            foreach (var block in doc.Elements.OfType<BlockElement>())
                if (!result.ContainsKey(block.BlockId)) { var package = _blocks.Load(block.BlockId); result.Add(block.BlockId,package); Visit(package.Document); }
        }
        Visit(_live); _blockPrintSnapshot = result;
    }
}
