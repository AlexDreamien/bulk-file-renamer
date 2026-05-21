using System;
using System.Collections.Generic;

namespace BulkFileRenamer.Core;

/// <summary>A pair of operations whose destinations collide.</summary>
public sealed record Conflict(int IndexA, int IndexB, string Path);

public static class ConflictDetector
{
    /// <summary>Find rename operations whose destination paths collide pairwise.</summary>
    public static IReadOnlyList<Conflict> Find(IReadOnlyList<RenameOperation> operations)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<Conflict>();
        for (var i = 0; i < operations.Count; i++)
        {
            var path = operations[i].NewFullPath;
            if (seen.TryGetValue(path, out var prior))
            {
                conflicts.Add(new Conflict(prior, i, path));
            }
            else
            {
                seen[path] = i;
            }
        }
        return conflicts;
    }
}
