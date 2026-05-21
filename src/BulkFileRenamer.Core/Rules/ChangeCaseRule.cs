using System.Globalization;

namespace BulkFileRenamer.Core.Rules;

public enum CaseMode
{
    Lower,
    Upper,
    Title,
}

public sealed class ChangeCaseRule : IRenameRule
{
    public CaseMode Mode { get; init; } = CaseMode.Lower;

    public string Apply(string stem, int index) => Mode switch
    {
        CaseMode.Lower => stem.ToLowerInvariant(),
        CaseMode.Upper => stem.ToUpperInvariant(),
        CaseMode.Title => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stem.ToLowerInvariant()),
        _ => stem,
    };
}
