using System;
using System.Collections.Generic;
using System.IO;

namespace BulkFileRenamer.Core;

/// <summary>A pair of operations whose destinations collide.</summary>
public sealed record Conflict(int IndexA, int IndexB, string Path);

public static class ConflictDetector
{
    /// <summary>
    /// Find rename operations whose destination paths collide pairwise,
    /// or whose destination escapes the source file's directory (path traversal).
    /// </summary>
    public static IReadOnlyList<Conflict> Find(IReadOnlyList<RenameOperation> operations)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<Conflict>();

        for (var i = 0; i < operations.Count; i++)
        {
            var op = operations[i];
            var newPath = op.NewFullPath;

            // Normalise both paths before comparing directories so that
            // sequences like "dir\..\other" are resolved consistently.
            var sourceDir = NormalisedDirectory(op.Source.FullPath);
            var targetDir = NormalisedDirectory(newPath);

            if (!string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase))
            {
                // Treat path-traversal as a self-conflict (same index for both slots).
                conflicts.Add(new Conflict(i, i, newPath));
                continue;
            }

            if (seen.TryGetValue(newPath, out var prior))
            {
                conflicts.Add(new Conflict(prior, i, newPath));
            }
            else
            {
                seen[newPath] = i;
            }
        }

        return conflicts;
    }

    private static string NormalisedDirectory(string fullPath)
    {
        // GetFullPath resolves "..\" sequences; GetDirectoryName strips the file name.
        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(fullPath)) ?? string.Empty;
        }
        catch (Exception)
        {
            // If the path is malformed, treat the directory as empty so it
            // will never match the source's (valid) directory.
            return string.Empty;
        }
    }
}
