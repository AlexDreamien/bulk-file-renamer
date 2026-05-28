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

    [Fact]
    public void Case_only_rename_changes_casing_on_real_filesystem()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "bfr_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(tmp);
        try
        {
            var original = Path.Combine(tmp, "Report.txt");
            File.WriteAllText(original, "hello");

            var items = new[] { new FileItem(original) };
            var pipeline = new RenamePipeline();
            pipeline.Rules.Add(new ChangeCaseRule { Mode = CaseMode.Lower });
            var ops = RenamePlanner.Plan(items, pipeline);

            var executor = new RenameExecutor();
            var executed = executor.Execute(ops);

            // After rename, actual on-disk name must be lower-case.
            var files = Directory.GetFiles(tmp);
            Assert.Single(files);
            Assert.Equal("report.txt", Path.GetFileName(files[0]),
                StringComparer.Ordinal);

            // Undo must restore original casing.
            executor.Undo(executed);
            files = Directory.GetFiles(tmp);
            Assert.Single(files);
            Assert.Equal("Report.txt", Path.GetFileName(files[0]),
                StringComparer.Ordinal);
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }
}

public class PartialFailureTests
{
    private sealed class FaultingMover : IFileMover
    {
        private readonly int _failOnIndex;
        private int _callCount;
        public List<(string From, string To)> Moves { get; } = new();

        public FaultingMover(int failOnIndex) => _failOnIndex = failOnIndex;

        public void Reset()
        {
            _callCount = int.MinValue; // will never match _failOnIndex again
            Moves.Clear();
        }

        public void Move(string from, string to)
        {
            if (_callCount == _failOnIndex)
                throw new IOException("Simulated failure");
            _callCount++;
            Moves.Add((from, to));
        }
    }

    [Fact]
    public void Execute_throws_RenameExecutionException_with_completed_moves()
    {
        // Mover fails on the 2nd call (index 1), so op[0] succeeds, op[1] fails.
        var mover = new FaultingMover(failOnIndex: 1);
        var executor = new RenameExecutor(mover);
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "1.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "2.txt")),
            new(new FileItem(Path.Combine("C:", "c.txt")), Path.Combine("C:", "3.txt")),
        };

        var ex = Assert.Throws<RenameExecutionException>(() => executor.Execute(ops));

        Assert.Single(ex.Executed);
        Assert.Equal(Path.Combine("C:", "a.txt"), ex.Executed[0].OldPath);
        Assert.Equal(Path.Combine("C:", "1.txt"), ex.Executed[0].NewPath);
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Fact]
    public void Undo_after_partial_failure_reverses_completed_moves()
    {
        var mover = new FaultingMover(failOnIndex: 1);
        var executor = new RenameExecutor(mover);
        var ops = new List<RenameOperation>
        {
            new(new FileItem(Path.Combine("C:", "a.txt")), Path.Combine("C:", "1.txt")),
            new(new FileItem(Path.Combine("C:", "b.txt")), Path.Combine("C:", "2.txt")),
        };

        var ex = Assert.Throws<RenameExecutionException>(() => executor.Execute(ops));

        // Pretend caller saved the partial batch; now undo it.
        // Reset the mover so it no longer faults on subsequent calls.
        mover.Reset();
        executor.Undo(ex.Executed);

        Assert.Single(mover.Moves);
        Assert.Equal((Path.Combine("C:", "1.txt"), Path.Combine("C:", "a.txt")), mover.Moves[0]);
    }
}

public class PathTraversalConflictTests
{
    [Fact]
    public void Path_traversal_via_prefix_is_flagged_as_conflict()
    {
        // Simulate PrefixRule producing "..\..\Windows\Report" for a file in C:\tmp\
        var sourceDir = Path.Combine("C:", "tmp");
        var source = Path.Combine(sourceDir, "report.txt");
        // The new path escapes the directory — e.g. attacker prefix "..\..\Windows\"
        var traversalTarget = Path.GetFullPath(Path.Combine(sourceDir, @"..\..\Windows\report.txt"));
        var ops = new List<RenameOperation>
        {
            new(new FileItem(source), traversalTarget),
        };

        var conflicts = ConflictDetector.Find(ops);

        Assert.Single(conflicts);
        Assert.Equal(0, conflicts[0].IndexA);
        Assert.Equal(0, conflicts[0].IndexB);
    }

    [Fact]
    public void Normal_same_directory_rename_is_not_flagged()
    {
        var source = Path.Combine("C:", "tmp", "report.txt");
        var dest = Path.Combine("C:", "tmp", "REPORT.txt");
        var ops = new List<RenameOperation>
        {
            new(new FileItem(source), dest),
        };

        Assert.Empty(ConflictDetector.Find(ops));
    }

    [Fact]
    public void Apply_blocked_when_path_traversal_conflict_present()
    {
        // ConflictDetector.Find returns non-empty → HasConflicts = true → CanApply = false.
        // Verify at the Core level: RenamePlanner + ConflictDetector together.
        var sourceDir = Path.Combine("C:", "tmp");
        var source = Path.Combine(sourceDir, "report.txt");
        var traversalTarget = Path.GetFullPath(Path.Combine(sourceDir, @"..\..\Windows\report.txt"));
        var ops = new List<RenameOperation>
        {
            new(new FileItem(source), traversalTarget),
        };

        var conflicts = ConflictDetector.Find(ops);
        Assert.NotEmpty(conflicts);
    }
}
