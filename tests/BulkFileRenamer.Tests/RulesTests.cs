using BulkFileRenamer.Core;
using BulkFileRenamer.Core.Rules;
using Xunit;

namespace BulkFileRenamer.Tests;

public class SearchAndReplaceRuleTests
{
    [Fact]
    public void Replaces_every_occurrence()
    {
        var rule = new SearchAndReplaceRule { Find = "old", Replace = "new" };
        Assert.Equal("new_file_new", rule.Apply("old_file_old", 0));
    }

    [Fact]
    public void Empty_find_is_a_noop()
    {
        var rule = new SearchAndReplaceRule { Find = "", Replace = "x" };
        Assert.Equal("unchanged", rule.Apply("unchanged", 0));
    }

    [Fact]
    public void Case_insensitive_match_when_requested()
    {
        var rule = new SearchAndReplaceRule { Find = "IMG", Replace = "photo", MatchCase = false };
        Assert.Equal("photo_001", rule.Apply("img_001", 0));
    }

    [Fact]
    public void Case_sensitive_by_default()
    {
        var rule = new SearchAndReplaceRule { Find = "IMG", Replace = "photo" };
        Assert.Equal("img_001", rule.Apply("img_001", 0));
    }
}

public class PrefixSuffixRuleTests
{
    [Fact]
    public void PrefixRule_prepends_text()
    {
        Assert.Equal("backup_report", new PrefixRule { Text = "backup_" }.Apply("report", 0));
    }

    [Fact]
    public void SuffixRule_appends_text()
    {
        Assert.Equal("report_v2", new SuffixRule { Text = "_v2" }.Apply("report", 0));
    }
}

public class NumberingRuleTests
{
    [Fact]
    public void Pads_with_leading_zeros()
    {
        var rule = new NumberingRule { Start = 1, Padding = 3 };
        Assert.Equal("001_file", rule.Apply("file", 0));
        Assert.Equal("002_file", rule.Apply("file", 1));
    }

    [Fact]
    public void Custom_start_and_step()
    {
        var rule = new NumberingRule { Start = 100, Step = 10, Padding = 4 };
        Assert.Equal("0100_x", rule.Apply("x", 0));
        Assert.Equal("0110_x", rule.Apply("x", 1));
        Assert.Equal("0120_x", rule.Apply("x", 2));
    }

    [Fact]
    public void Suffix_position_appends_number()
    {
        var rule = new NumberingRule { Position = NumberingPosition.Suffix, Padding = 2 };
        Assert.Equal("file_01", rule.Apply("file", 0));
    }

    [Fact]
    public void Custom_separator()
    {
        var rule = new NumberingRule { Padding = 2, Separator = "-" };
        Assert.Equal("01-file", rule.Apply("file", 0));
    }
}

public class ChangeCaseRuleTests
{
    [Theory]
    [InlineData(CaseMode.Lower, "MixedCase", "mixedcase")]
    [InlineData(CaseMode.Upper, "MixedCase", "MIXEDCASE")]
    [InlineData(CaseMode.Title, "hello world", "Hello World")]
    public void Applies_chosen_mode(CaseMode mode, string input, string expected)
    {
        var rule = new ChangeCaseRule { Mode = mode };
        Assert.Equal(expected, rule.Apply(input, 0));
    }
}

public class RemoveCharactersRuleTests
{
    [Fact]
    public void Drops_only_listed_characters()
    {
        var rule = new RemoveCharactersRule { Characters = " _-" };
        Assert.Equal("hellothere", rule.Apply("hello _-there", 0));
    }

    [Fact]
    public void Empty_set_is_noop()
    {
        var rule = new RemoveCharactersRule();
        Assert.Equal("unchanged", rule.Apply("unchanged", 0));
    }
}

public class RenamePipelineTests
{
    [Fact]
    public void Applies_rules_in_order()
    {
        var pipeline = new RenamePipeline();
        pipeline.Rules.Add(new SearchAndReplaceRule { Find = "old", Replace = "new" });
        pipeline.Rules.Add(new SuffixRule { Text = "_v2" });
        Assert.Equal("new_file_v2", pipeline.ApplyToStem("old_file", 0));
    }

    [Fact]
    public void Empty_pipeline_is_identity()
    {
        var pipeline = new RenamePipeline();
        Assert.Equal("anything", pipeline.ApplyToStem("anything", 0));
    }

    [Fact]
    public void Preview_combines_dir_new_stem_and_extension()
    {
        var pipeline = new RenamePipeline();
        pipeline.Rules.Add(new PrefixRule { Text = "renamed_" });
        var item = new FileItem(System.IO.Path.Combine("C:", "tmp", "report.txt"));
        Assert.Equal(
            System.IO.Path.Combine("C:", "tmp", "renamed_report.txt"),
            pipeline.PreviewFor(item, 0));
    }
}
