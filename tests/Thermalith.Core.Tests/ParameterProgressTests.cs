using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Thermalith.Core.Rendering;
using Xunit;

namespace Thermalith.Core.Tests;

public sealed class ParameterProgressTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Thermalith-progress-" + Guid.NewGuid().ToString("N"));
    private string StorePath => Path.Combine(_folder, "progress.json");
    private static LabelParameter Parameter => new() { Name = "ID", IncrementEnabled = true, Start = 26, Format = "Alphabetic", Step = 1 };

    [Fact]
    public void Reopening_resumes_after_completed_labels_without_resaving_artwork()
    {
        var store = new ParameterProgressStore(StorePath);
        var definitions = new List<LabelParameter> { Parameter };
        store.SaveNext("doc", definitions, store.Resume("doc", definitions), 2);
        var reopened = new ParameterProgressStore(StorePath).Resume("doc", definitions);
        Assert.Equal("AB", reopened[0].ValueAt(0));
        store.SaveNext("doc", definitions, reopened, 1);
        Assert.Equal("AC", new ParameterProgressStore(StorePath).Resume("doc", definitions)[0].ValueAt(0));
    }

    [Fact]
    public void Cancellation_preserves_only_confirmed_labels_and_zero_does_not_advance()
    {
        var store = new ParameterProgressStore(StorePath);
        var defs = new List<LabelParameter> { Parameter };
        store.SaveNext("doc", defs, defs, 0);
        Assert.Equal("Z", store.Resume("doc", defs)[0].ValueAt(0));
        store.SaveNext("doc", defs, defs, 1);
        Assert.Equal("AA", store.Resume("doc", defs)[0].ValueAt(0));
    }

    [Fact]
    public void Explicit_overrides_and_different_documents_are_independent()
    {
        var store = new ParameterProgressStore(StorePath);
        var defs = new List<LabelParameter> { Parameter };
        store.SaveNext("doc", defs, defs, 2);
        Assert.Equal("Z", store.Resume("other-doc", defs)[0].ValueAt(0));
        Assert.Equal("A", store.Resume("doc", [Parameter with { Start = 1 }])[0].ValueAt(0));
        Assert.Equal("Z", store.Resume("doc", [Parameter with { ProgressRevision = "user-reset" }])[0].ValueAt(0));
        Assert.Equal("fixed", store.Resume("doc", [Parameter with { IncrementEnabled = false, Prefix = "fixed" }])[0].ValueAt(0));
    }

    [Fact]
    public void Preview_reference_mode_changes_text_but_preserves_QR_payload()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Preview" }, Canvas = new Canvas { WidthMm = 50, HeightMm = 30 },
            Parameters = [Parameter], Elements = [
                new TextElement { Id = "text", Props = new TextProps { Content = "Item {ID}" } },
                new QrElement { Id = "qr", Props = new QrProps { Value = "{ID}" } }
            ] };
        var refs = LabelResolver.Resolve(doc, new ResolveContext { ShowParameterReferences = true });
        Assert.Equal("Item {ID}", Assert.IsType<ResolvedText>(refs.Elements[0]).Text);
        Assert.Equal("Z", Assert.IsType<ResolvedQr>(refs.Elements[1]).Value);
        Assert.Equal("Item Z", Assert.IsType<ResolvedText>(LabelResolver.Resolve(doc, new ResolveContext()).Elements[0]).Text);
    }

    [Fact]
    public void Escaped_parameter_references_are_not_treated_as_active_sequences()
    {
        var doc = new LabelDocument { Metadata = new LabelMetadata { Name = "Literal" }, Canvas = new Canvas { WidthMm = 50, HeightMm = 30 },
            Parameters = [Parameter], Elements = [new TextElement { Id = "text", Props = new TextProps { Content = "{{ID}}" } }] };
        Assert.Empty(Thermalith.Core.Tokens.TokenScanner.Scan(doc));
        Assert.Equal("{ID}", Assert.IsType<ResolvedText>(LabelResolver.Resolve(doc, new ResolveContext()).Elements[0]).Text);
    }
    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}
