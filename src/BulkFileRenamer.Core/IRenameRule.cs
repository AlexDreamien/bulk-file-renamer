namespace BulkFileRenamer.Core;

/// <summary>Transforms a file stem (name without extension) into a new stem.</summary>
public interface IRenameRule
{
    /// <param name="stem">The current stem (without extension).</param>
    /// <param name="index">Zero-based index of the file in the current batch.</param>
    string Apply(string stem, int index);
}
