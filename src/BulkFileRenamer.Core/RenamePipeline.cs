using System.Collections.Generic;
using System.IO;

namespace BulkFileRenamer.Core;

/// <summary>Sequential composition of <see cref="IRenameRule"/>s.</summary>
public sealed class RenamePipeline
{
    public IList<IRenameRule> Rules { get; } = new List<IRenameRule>();

    public string ApplyToStem(string stem, int index)
    {
        var current = stem;
        foreach (var rule in Rules) current = rule.Apply(current, index);
        return current;
    }

    /// <summary>Compute the full new path for <paramref name="item"/> at position <paramref name="index"/>.</summary>
    public string PreviewFor(FileItem item, int index)
    {
        var newStem = ApplyToStem(item.Stem, index);
        return Path.Combine(item.Directory, newStem + item.Extension);
    }
}
