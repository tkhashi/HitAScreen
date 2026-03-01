using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public sealed class HintLabelGenerator
{
    public IReadOnlyList<string> Generate(int candidateCount, string characterSet)
    {
        if (candidateCount <= 0)
        {
            return Array.Empty<string>();
        }

        var symbols = NormalizeCharacterSet(characterSet);
        var labels = new string[candidateCount];

        for (var index = 0; index < candidateCount; index++)
        {
            labels[index] = ToLabel(index, symbols);
        }

        return labels;
    }

    public static IReadOnlyList<OverlayHint> ApplyInputFilter(IEnumerable<(string Label, UiCandidate Candidate)> entries, string input)
    {
        var upperInput = input.ToUpperInvariant();
        return entries
            .Select(entry => new OverlayHint(
                entry.Label,
                entry.Candidate.Bounds,
                entry.Candidate.Title,
                entry.Label.StartsWith(upperInput, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static char[] NormalizeCharacterSet(string characterSet)
    {
        var symbols = characterSet
            .ToUpperInvariant()
            .Where(static ch => !char.IsWhiteSpace(ch))
            .Distinct()
            .ToArray();

        if (symbols.Length == 0)
        {
            return "ASDFGHJKL".ToCharArray();
        }

        return symbols;
    }

    private static string ToLabel(int zeroBasedIndex, char[] symbols)
    {
        var baseCount = symbols.Length;
        var value = zeroBasedIndex;
        Span<char> buffer = stackalloc char[16];
        var write = buffer.Length;

        do
        {
            var remainder = value % baseCount;
            buffer[--write] = symbols[remainder];
            value = (value / baseCount) - 1;
        }
        while (value >= 0 && write > 0);

        return new string(buffer[write..]);
    }
}
