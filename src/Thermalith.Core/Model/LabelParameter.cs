using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Thermalith.Core.Model;

/// <summary>A reusable label value. Index zero is the first physical label in a print run.</summary>
public sealed record LabelParameter
{
    public string Name { get; init; } = "Parameter1";
    public string Prefix { get; init; } = "";
    public string Suffix { get; init; } = "";
    public bool IncrementEnabled { get; init; }
    public string Format { get; init; } = "Numeric";
    public long Start { get; init; } = 1;
    public int Step { get; init; } = 1;
    public int Padding { get; init; } = 1;
    public string ProgressRevision { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Id { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? PreviousNames { get; init; }
    public LabelParameter WithName(string name) => this with
    {
        Name = name,
        PreviousNames = name == Name || Id is null ? PreviousNames : (PreviousNames ?? []).Append(Name).Where(n => n != name).Distinct(StringComparer.Ordinal).ToList()
    };

    public string ValueAt(int index)
    {
        if (!IncrementEnabled) return Prefix + Suffix;
        var value = checked((long)Start + (long)Math.Max(0, index) * Step);
        var text = Format switch
        {
            "Alphabetic" => Alphabetic(value),
            "Roman" => Roman(value),
            _ => value.ToString(CultureInfo.InvariantCulture).PadLeft(Math.Clamp(Padding, 1, 20), '0'),
        };
        return Prefix + text + Suffix;
    }

    public void Validate()
    {
        if (!Regex.IsMatch(Name, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new ArgumentException("Names must start with a letter or underscore and contain only letters, digits or underscores.");
        if (Start < 1 || Step < 1) throw new ArgumentException("Start and step must be at least 1.");
        if (Padding is < 1 or > 20) throw new ArgumentException("Digits must be between 1 and 20.");
        if (Format is not ("Numeric" or "Alphabetic" or "Roman")) throw new ArgumentException("Choose a numbering format.");
    }

    private static string Alphabetic(long n)
    {
        if (n < 1) throw new ArgumentOutOfRangeException(nameof(n));
        var result = new StringBuilder();
        while (n > 0) { n--; result.Insert(0, (char)('A' + n % 26)); n /= 26; }
        return result.ToString();
    }

    private static string Roman(long n)
    {
        // Standard Roman numerals up to 3999; parenthesized groups extend the sequence by thousands.
        if (n < 1) throw new ArgumentOutOfRangeException(nameof(n));
        if (n > 3999) return "(" + Roman(n / 1000) + ")" + (n % 1000 == 0 ? "" : Roman(n % 1000));
        var result = new StringBuilder();
        foreach (var (value, glyph) in new[] { (1000,"M"), (900,"CM"), (500,"D"), (400,"CD"), (100,"C"), (90,"XC"), (50,"L"), (40,"XL"), (10,"X"), (9,"IX"), (5,"V"), (4,"IV"), (1,"I") })
            while (n >= value) { result.Append(glyph); n -= value; }
        return result.ToString();
    }
}
