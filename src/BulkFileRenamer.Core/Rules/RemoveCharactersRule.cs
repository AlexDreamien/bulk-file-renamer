using System.Collections.Generic;
using System.Text;

namespace BulkFileRenamer.Core.Rules;

/// <summary>Drops every character that appears in <see cref="Characters"/>.</summary>
public sealed class RemoveCharactersRule : IRenameRule
{
    public string Characters { get; init; } = string.Empty;

    public string Apply(string stem, int index)
    {
        if (string.IsNullOrEmpty(Characters)) return stem;
        var blocked = new HashSet<char>(Characters);
        var sb = new StringBuilder(stem.Length);
        foreach (var c in stem)
        {
            if (!blocked.Contains(c)) sb.Append(c);
        }
        return sb.ToString();
    }
}
