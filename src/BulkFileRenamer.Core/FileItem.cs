using System.IO;

namespace BulkFileRenamer.Core;

/// <summary>Lightweight wrapper around a file path that exposes its parts.</summary>
public sealed record FileItem(string FullPath)
{
    public string Directory => Path.GetDirectoryName(FullPath) ?? string.Empty;
    public string Stem => Path.GetFileNameWithoutExtension(FullPath);
    public string Extension => Path.GetExtension(FullPath);
    public string FileName => Path.GetFileName(FullPath);
}
