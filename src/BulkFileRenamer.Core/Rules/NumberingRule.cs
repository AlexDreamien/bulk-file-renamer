using System.Globalization;

namespace BulkFileRenamer.Core.Rules;

public enum NumberingPosition
{
    Prefix,
    Suffix,
}

/// <summary>Adds a sequential number to each file's stem.</summary>
public sealed class NumberingRule : IRenameRule
{
    public int Start { get; init; } = 1;
    public int Step { get; init; } = 1;
    public int Padding { get; init; } = 3;
    public NumberingPosition Position { get; init; } = NumberingPosition.Prefix;
    public string Separator { get; init; } = "_";

    public string Apply(string stem, int index)
    {
        var n = Start + index * Step;
        var formatted = n.ToString(CultureInfo.InvariantCulture)
            .PadLeft(Padding, '0');
        return Position == NumberingPosition.Prefix
            ? formatted + Separator + stem
            : stem + Separator + formatted;
    }
}
