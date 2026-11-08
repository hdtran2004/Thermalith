using Thermalith.Core.Model;
namespace Thermalith.Core.Tokens;

/// <summary>Expand named parameter dependencies once per label, preserving escaped braces.</summary>
public static class ParameterResolver
{
    public static IReadOnlyDictionary<string, string> ResolveAll(IEnumerable<LabelParameter>? parameters, int labelIndex = 0)
    {
        var definitions = (parameters ?? []).ToDictionary(p => p.Name, StringComparer.Ordinal);
        var aliases = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var p in definitions.Values) foreach (var alias in p.PreviousNames ?? [])
        {
            if (definitions.ContainsKey(alias) && alias != p.Name) throw new FormatException("Parameter name '" + alias + "' is reserved by renamed parameter '" + p.Name + "'. Choose another name.");
            if (!definitions.ContainsKey(alias)) aliases.TryAdd(alias,p.Name);
        }
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = new List<string>();
        string Visit(string name)
        {
            if (result.TryGetValue(name, out var cached)) return cached;
            if (aliases.TryGetValue(name,out var canonical)) { var resolved = Visit(canonical); result[name] = resolved; return resolved; }
            if (path.Contains(name)) throw new FormatException("Circular parameter reference: " + string.Join(" → ", path.Append(name)));
            if (!definitions.TryGetValue(name, out var parameter)) throw new FormatException($"Parameter '{path.LastOrDefault()}' references undefined parameter '{name}'.");
            if (path.Count >= 64) throw new FormatException("Parameter nesting exceeds 64 levels.");
            path.Add(name);
            var raw = parameter.ValueAt(labelIndex);
            foreach (var dependency in TokenResolver.ReferencedNames(raw)) Visit(dependency);
            var estimatedSize = (long)raw.Length;
            foreach (var dependency in TokenResolver.ReferencedNames(raw)) estimatedSize += result[dependency].Length;
            if (estimatedSize > 1_000_000) throw new FormatException($"Expanded parameter '{name}' is too long.");
            var value = new TokenResolver(null, parameters: result).Substitute(raw);
            path.RemoveAt(path.Count - 1);
            result[name] = value;
            return value;
        }
        foreach (var name in definitions.Keys) Visit(name);
        foreach (var alias in aliases.Keys) Visit(alias);
        return result;
    }

    /// <summary>Parameters used by the artwork, including indirect dependencies and counters.</summary>
    public static HashSet<string> UsedNames(IEnumerable<LabelParameter>? parameters, IEnumerable<string> roots)
    {
        var definitions = (parameters ?? []).ToDictionary(p => p.Name, StringComparer.Ordinal);
        var aliases = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach (var p in definitions.Values) foreach (var alias in p.PreviousNames ?? [])
        {
            if (definitions.ContainsKey(alias) && alias != p.Name) throw new FormatException("Parameter name '" + alias + "' is reserved by renamed parameter '" + p.Name + "'. Choose another name.");
            if (!definitions.ContainsKey(alias)) aliases.TryAdd(alias,p.Name);
        }
        var used = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string name)
        {
            if (aliases.TryGetValue(name,out var canonical)) name = canonical;
            if (!definitions.TryGetValue(name, out var p) || !used.Add(name)) return;
            foreach (var reference in TokenResolver.ReferencedNames(p.Prefix + p.Suffix)) Visit(reference);
        }
        foreach (var root in roots) Visit(root);
        return used;
    }
}
