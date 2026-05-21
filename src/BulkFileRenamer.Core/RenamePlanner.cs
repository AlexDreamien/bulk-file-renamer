using System.Collections.Generic;

namespace BulkFileRenamer.Core;

public static class RenamePlanner
{
    public static IReadOnlyList<RenameOperation> Plan(
        IReadOnlyList<FileItem> items,
        RenamePipeline pipeline)
    {
        var ops = new List<RenameOperation>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            ops.Add(new RenameOperation(items[i], pipeline.PreviewFor(items[i], i)));
        }
        return ops;
    }
}
