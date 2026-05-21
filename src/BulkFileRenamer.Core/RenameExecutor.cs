using System.Collections.Generic;
using System.IO;

namespace BulkFileRenamer.Core;

/// <summary>Records a single executed rename so it can be undone.</summary>
public sealed record ExecutedRename(string OldPath, string NewPath);

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
            _mover.Move(op.Source.FullPath, op.NewFullPath);
            executed.Add(new ExecutedRename(op.Source.FullPath, op.NewFullPath));
        }
        return executed;
    }

    /// <summary>Reverse a previously executed batch, in reverse order to handle chains.</summary>
    public void Undo(IReadOnlyList<ExecutedRename> executed)
    {
        for (var i = executed.Count - 1; i >= 0; i--)
        {
            _mover.Move(executed[i].NewPath, executed[i].OldPath);
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
