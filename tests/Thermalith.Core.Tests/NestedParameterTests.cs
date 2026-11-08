using Thermalith.Core.Model;
using Thermalith.Core.Tokens;
using Xunit;
namespace Thermalith.Core.Tests;
public class NestedParameterTests
{
    [Fact]
    public void Nested_references_share_one_counter_value_and_preserve_escaped_braces()
    {
        var parameters = new[] {
            new LabelParameter { Name = "Project", Prefix = "FP-280" },
            new LabelParameter { Name = "Counter", IncrementEnabled = true, Format = "Alphabetic", Start = 26 },
            new LabelParameter { Name = "Serial", Prefix = "{Project}-JB1-{Counter}", Suffix = "-{{literal}}" },
            new LabelParameter { Name = "Wrapped", Prefix = "{Serial}/{Counter}" }
        };
        Assert.Equal("FP-280-JB1-AA-{literal}/AA", ParameterResolver.ResolveAll(parameters,1)["Wrapped"]);
        Assert.Equal(new[] { "Counter", "Project", "Serial", "Wrapped" }, ParameterResolver.UsedNames(parameters,["Wrapped"]).OrderBy(n=>n));
    }
    [Fact]
    public void Circular_and_missing_references_are_rejected()
    {
        Assert.Contains("A → B → A", Assert.Throws<FormatException>(() => ParameterResolver.ResolveAll([
            new LabelParameter { Name = "A", Prefix = "{B}" }, new LabelParameter { Name = "B", Prefix = "{A}" }])).Message);
        Assert.Contains("undefined", Assert.Throws<FormatException>(() => ParameterResolver.ResolveAll([new LabelParameter { Name = "A", Prefix = "{Missing}" }])).Message);
        Assert.Throws<FormatException>(() => ParameterResolver.ResolveAll([new LabelParameter { Name = "A", Prefix = "{A}" }]));
    }
    [Fact]
    public void Referenced_counter_keeps_its_own_padding_step_and_suffix()
    {
        var values = ParameterResolver.ResolveAll([
            new LabelParameter { Name = "Number", IncrementEnabled = true, Start = 5, Step = 2, Padding = 4 },
            new LabelParameter { Name = "ID", Prefix = "P-{Number}", Suffix = "-{Number}" }
        ],2);
        Assert.Equal("P-0009-0009",values["ID"]);
    }
}
