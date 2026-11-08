using System.Security.Cryptography;
using System.Text;
namespace Thermalith.Core.Model;
public static class ParameterIdentity
{
    public static List<LabelParameter> Assign(IEnumerable<LabelParameter>? parameters,string scope) => (parameters ?? []).Select(p =>
        string.IsNullOrEmpty(p.Id) ? p with { Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope + "\0" + p.Name)))[..32] } : p).ToList();
    public static List<LabelParameter> PreserveNames(IEnumerable<LabelParameter> updated,IReadOnlyList<LabelParameter> previous,string scope)
    {
        return Assign(updated,scope).Select(p =>
        {
            var old = previous.FirstOrDefault(o => o.Id == p.Id);
            return old is null || old.Name == p.Name ? p : p with { PreviousNames = (p.PreviousNames ?? []).Concat(old.PreviousNames ?? []).Append(old.Name).Where(n => n != p.Name).Distinct(StringComparer.Ordinal).ToList() };
        }).ToList();
    }
}
