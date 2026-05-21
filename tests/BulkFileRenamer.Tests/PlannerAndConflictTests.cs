using System.Collections.Generic;
using System.IO;
using BulkFileRenamer.Core;
using BulkFileRenamer.Core.Rules;
using Xunit;

namespace BulkFileRenamer.Tests;

public class RenamePlannerTests
{
    [Fact]
    public void Plan_returns_one_operation_per_item_in_order()
    {
        var items = new[]
        {
            new FileItem(Path.Combine("C:", "tmp", "a.txt")),
            new FileItem(Path.Combine("C:", "tmp", "b.txt")),
        };
        var pipeline = new RenamePipeline();
        pipeline.Rules.Add(new PrefixRule { Text = "renamed_" });

        var ops = RenamePlanner.Plan(items, pipeline);

        Assert.Equal(2, ops.Count);
        Assert.Equal(Path.Combine("C:", "tmp", "renamed_a.txt"), ops[0].NewFullPath);
        Assert.Equal(Path.Combine("C:", "tmp", "renamed_b.txt"), ops[1].NewFullPath);
    }

    [Fact]
    public void Noop_flagged_when_pipeline_does_not_change_name()
    {
        var items = new[] { new FileItem(Path.Combine("C:", "tmp", "report.txt")) };
        var ops = RenamePlanner.Plan(items, new RenamePipeline());
        Assert.True(ops[0].IsNoop);
    }
}

public class ConflictDetectorTests
{
    [Fact]
    public void No_conflicts_when_all_destinations_unique()
    {
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "1.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "2.txt")),
        };
        Assert.Empty(ConflictDetector.Find(ops));
    }

    [Fact]
    public void Reports_collision_with_indices_of_both_sources()
    {
        var dest = Path.Combine("C:", "merged.txt");
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), dest),
            new(new FileItem(Path.Combine("C:", "b.txt")), dest),
        };
        var conflicts = ConflictDetector.Find(ops);
        Assert.Single(conflicts);
        Assert.Equal(0, conflicts[0].IndexA);
        Assert.Equal(1, conflicts[0].IndexB);
        Assert.Equal(dest, conflicts[0].Path);
    }

    [Fact]
    public void Detects_three_way_collision_as_two_pairs()
    {
        var dest = Path.Combine("C:", "x.txt");
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), dest),
            new(new FileItem(Path.Combine("C:", "b.txt")), dest),
            new(new FileItem(Path.Combine("C:", "c.txt")), dest),
        };
        var conflicts = ConflictDetector.Find(ops);
        Assert.Equal(2, conflicts.Count);
    }

    [Fact]
    public void Path_comparison_is_case_insensitive()
    {
        // Windows file system semantics; OrdinalIgnoreCase
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "Report.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "report.txt")),
        };
        Assert.Single(ConflictDetector.Find(ops));
    }
}

public class RenameExecutorTests
{
    private sealed class RecordingMover : IFileMover
    {
        public List<(string From, string To)> Moves { get; } = new();
        public void Move(string from, string to) => Moves.Add((from, to));
    }

    [Fact]
    public void Execute_invokes_mover_for_each_real_change()
    {
        var mover = new RecordingMover();
        var executor = new RenameExecutor(mover);
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "1.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "2.txt")),
        };

        var executed = executor.Execute(ops);

        Assert.Equal(2, executed.Count);
        Assert.Equal(2, mover.Moves.Count);
        Assert.Equal((Path.Combine("C:", "a.txt"), Path.Combine("C:", "1.txt")), mover.Moves[0]);
    }

    [Fact]
    public void Execute_skips_noops()
    {
        var mover = new RecordingMover();
        var executor = new RenameExecutor(mover);
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "a.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "2.txt")),
        };
        executor.Execute(ops);
        Assert.Single(mover.Moves);
    }

    [Fact]
    public void Undo_reverses_in_opposite_order()
    {
        var mover = new RecordingMover();
        var executor = new RenameExecutor(mover);
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "1.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "2.txt")),
        };

        var executed = executor.Execute(ops);
        mover.Moves.Clear();
        executor.Undo(executed);

        Assert.Equal(2, mover.Moves.Count);
        // Last executed is undone first
        Assert.Equal((Path.Combine("C:", "2.txt"), Path.Combine("C:", "b.txt")), mover.Moves[0]);
        Assert.Equal((Path.Combine("C:", "1.txt"), Path.Combine("C:", "a.txt")), mover.Moves[1]);
    }
}

public class RenameExecutorIntegrationTests
{
    [Fact]
    public void Real_filesystem_round_trip()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "bfr_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tmp);
        try
        {
            var a = Path.Combine(tmp, "alpha.txt");
            var b = Path.Combine(tmp, "beta.txt");
            File.WriteAllText(a, "1");
            File.WriteAllText(b, "2");

            var items = new[] { new FileItem(a), new FileItem(b) };
            var pipeline = new RenamePipeline();
            pipeline.Rules.Add(new PrefixRule { Text = "renamed_" });
            var ops = RenamePlanner.Plan(items, pipeline);

            var executor = new RenameExecutor();
            var executed = executor.Execute(ops);

            Assert.True(File.Exists(Path.Combine(tmp, "renamed_alpha.txt")));
            Assert.True(File.Exists(Path.Combine(tmp, "renamed_beta.txt")));
            Assert.False(File.Exists(a));

            executor.Undo(executed);
            Assert.True(File.Exists(a));
            Assert.True(File.Exists(b));
            Assert.False(File.Exists(Path.Combine(tmp, "renamed_alpha.txt")));
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }
}
