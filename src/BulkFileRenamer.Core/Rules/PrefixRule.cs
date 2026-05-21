namespace BulkFileRenamer.Core.Rules;

public sealed class PrefixRule : IRenameRule
{
    public string Text { get; init; } = string.Empty;

    public string Apply(string stem, int index) => Text + stem;
}
