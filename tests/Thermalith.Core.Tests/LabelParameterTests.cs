using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;
using Xunit;

namespace Thermalith.Core.Tests;

public class LabelParameterTests
{
    [Theory]
    [InlineData("Numeric", 1, 0, "001")]
    [InlineData("Numeric", 9, 1, "010")]
    [InlineData("Alphabetic", 26, 0, "Z")]
    [InlineData("Alphabetic", 26, 1, "AA")]
    [InlineData("Alphabetic", 52, 1, "BA")]
    [InlineData("Roman", 1, 0, "I")]
    [InlineData("Roman", 3, 1, "IV")]
    [InlineData("Roman", 8, 1, "IX")]
    [InlineData("Roman", 3999, 1, "(IV)")]
    public void Sequences_roll_over_logically(string format, int start, int index, string expected)
    {
        var parameter = new LabelParameter { Format = format, Start = start, Padding = 3, IncrementEnabled = true, Prefix = "P-", Suffix = "-X" };
        Assert.Equal("P-" + expected + "-X", parameter.ValueAt(index));
    }

    [Fact]
    public void Disabled_increment_is_fixed_and_step_is_honoured()
    {
        var p = new LabelParameter { Prefix = "A", Suffix = "Z", IncrementEnabled = false, Step = 2 };
        Assert.Equal("AZ", p.ValueAt(100));
        Assert.Equal("A5Z", (p with { IncrementEnabled = true }).ValueAt(2));
    }

    [Fact]
    public void Saved_parameters_resolve_identically_in_text_QR_and_barcode_per_physical_label()
    {
        var doc = new LabelDocument
        {
            Metadata = new LabelMetadata { Name = "Parameters" },
            Canvas = new Canvas { WidthMm = 50, HeightMm = 30 },
            Parameters = [new LabelParameter { Name = "CableId", Prefix = "C-", IncrementEnabled = true, Format = "Alphabetic", Start = 26 }],
            Elements = [
                new TextElement { Id = "t", Props = new TextProps { Content = "{CableId}/{CableId}" } },
                new QrElement { Id = "q", Props = new QrProps { Value = "ID\n{CableId}" } },
                new BarcodeElement { Id = "b", Props = new BarcodeProps { Value = "{CableId}" } }
            ]
        };
        var saved = JsonSerializer.Serialize(doc, LabelJson.Options);
        var loaded = JsonSerializer.Deserialize<LabelDocument>(saved, LabelJson.Options)!;
        var result = LabelResolver.Resolve(loaded, new ResolveContext { RowIndex = 50, LabelIndex = 1, Data = new Dictionary<string, object?> { ["CableId"] = "override" } });
        Assert.Equal("C-AA/C-AA", Assert.IsType<ResolvedText>(result.Elements[0]).Text);
        Assert.Equal("ID\nC-AA", Assert.IsType<ResolvedQr>(result.Elements[1]).Value);
        Assert.Equal("C-AA", Assert.IsType<ResolvedBarcode>(result.Elements[2]).Value);
        Assert.DoesNotContain(new Thermalith.Core.Validation.LabelValidator().Validate(loaded).Diagnostics, d => d.Code == Thermalith.Core.Validation.ValidationCodes.UndeclaredToken);
        Assert.Equal("C-Z/C-Z", Assert.IsType<ResolvedText>(LabelResolver.Resolve(loaded, new ResolveContext()).Elements[0]).Text);
    }

    [Theory]
    [InlineData("", 1, 1)]
    [InlineData("A B", 1, 1)]
    [InlineData("1A", 1, 1)]
    [InlineData("Name", 0, 1)]
    [InlineData("Name", 1, 0)]
    public void Invalid_definitions_are_rejected(string name, int start, int step) =>
        Assert.Throws<ArgumentException>(() => new LabelParameter { Name = name, Start = start, Step = step }.Validate());
}
