using System;
using System.Collections.Generic;
using System.IO;

namespace BulkFileRenamer.Core;

/// <summary>Records a single executed rename so it can be undone.</summary>
public sealed record ExecutedRename(string OldPath, string NewPath);

/// <summary>
/// Thrown when <see cref="RenameExecutor.Execute"/> fails mid-batch.
/// The <see cref="Executed"/> list contains every rename that completed
/// before the failure so the caller can offer undo for those files.
/// </summary>
public sealed class RenameExecutionException : Exception
{
    public IReadOnlyList<ExecutedRename> Executed { get; }

    public RenameExecutionException(
        IReadOnlyList<ExecutedRename> executed,
        Exception innerException)
        : base(innerException.Message, innerException)
    {
        Executed = executed;
    }
}

/// <summary>Applies and reverses a planned batch of renames on the filesystem.</summary>
public sealed class RenameExecutor
{
    private readonly IFileMover _mover;

    public RenameExecutor() : this(new FileSystemMover()) { }

    /// <summary>Test seam: inject a fake mover that records actions without touching disk.</summary>
    public RenameExecutor(IFileMover mover)
    {
        _mover = mover;
    }

    public IReadOnlyList<ExecutedRename> Execute(IReadOnlyList<RenameOperation> operations)
    {
        var executed = new List<ExecutedRename>(operations.Count);
        foreach (var op in operations)
        {
            if (op.IsNoop) continue;
            try
            {
                MoveFile(op.Source.FullPath, op.NewFullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new RenameExecutionException(executed, ex);
            }
            executed.Add(new ExecutedRename(op.Source.FullPath, op.NewFullPath));
        }
        return executed;
    }

    /// <summary>Reverse a previously executed batch, in reverse order to handle chains.</summary>
    public void Undo(IReadOnlyList<ExecutedRename> executed)
    {
        for (var i = executed.Count - 1; i >= 0; i--)
        {
            MoveFile(executed[i].NewPath, executed[i].OldPath);
        }
    }

    private void MoveFile(string from, string to)
    {
        // On Windows/NTFS a case-only rename (paths equal ignoring case but differ in casing)
        // cannot be done in one step — File.Move would either silently no-op or throw.
        // Route through a temporary name in the same directory to force the casing change.
        var isCaseOnly = string.Equals(from, to, StringComparison.OrdinalIgnoreCase)
                      && !string.Equals(from, to, StringComparison.Ordinal);

        if (isCaseOnly)
        {
            var dir = Path.GetDirectoryName(from) ?? string.Empty;
            var temp = Path.Combine(dir, Path.GetFileNameWithoutExtension(from)
                                        + "_tmp_" + Guid.NewGuid().ToString("N")
                                        + Path.GetExtension(from));
            _mover.Move(from, temp);
            _mover.Move(temp, to);
        }
        else
        {
            _mover.Move(from, to);
        }
    }
}

public interface IFileMover
{
    void Move(string from, string to);
}

internal sealed class FileSystemMover : IFileMover
{
    public void Move(string from, string to) => File.Move(from, to);
}
