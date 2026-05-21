using System;

namespace BulkFileRenamer.Core.Rules;

/// <summary>Replaces every occurrence of <see cref="Find"/> with <see cref="Replace"/>.</summary>
public sealed class SearchAndReplaceRule : IRenameRule
{
    public string Find { get; init; } = string.Empty;
    public string Replace { get; init; } = string.Empty;
    public bool MatchCase { get; init; } = true;

    public string Apply(string stem, int index)
    {
        if (string.IsNullOrEmpty(Find)) return stem;
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return stem.Replace(Find, Replace, comparison);
    }
}
