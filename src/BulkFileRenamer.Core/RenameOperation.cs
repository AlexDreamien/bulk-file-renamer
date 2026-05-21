namespace BulkFileRenamer.Core;

/// <summary>A single planned rename: where the file is now and where it is going.</summary>
public sealed record RenameOperation(FileItem Source, string NewFullPath)
{
    public bool IsNoop => string.Equals(Source.FullPath, NewFullPath, System.StringComparison.Ordinal);
}
