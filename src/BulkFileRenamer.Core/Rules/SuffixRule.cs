namespace BulkFileRenamer.Core.Rules;

public sealed class SuffixRule : IRenameRule
{
    public string Text { get; init; } = string.Empty;

    public string Apply(string stem, int index) => stem + Text;
}
