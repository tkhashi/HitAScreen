using HitAScreen.Platform.Abstractions;

namespace HitAScreen.Core;

public sealed class HintLabelGenerator
{
    private const string HomeRowPriority = "ASDFGHJKL";

    public IReadOnlyList<string> Generate(int candidateCount, string characterSet)
    {
        if (candidateCount <= 0)
        {
            return Array.Empty<string>();
        }

        var symbols = NormalizeCharacterSet(characterSet);
        var labelLength = ResolveFixedLabelLength(candidateCount, symbols.Length);
        var labels = new string[candidateCount];

        for (var index = 0; index < candidateCount; index++)
        {
            labels[index] = ToFixedLengthLabel(index, symbols, labelLength);
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
        var normalized = characterSet
            .ToUpperInvariant()
            .Where(static ch => !char.IsWhiteSpace(ch))
            .Distinct()
            .ToArray();

        if (normalized.Length == 0)
        {
            normalized = HomeRowPriority.ToCharArray();
        }

        var prioritized = HomeRowPriority
            .Where(normalized.Contains)
            .Concat(normalized.Where(ch => !HomeRowPriority.Contains(ch)))
            .Distinct()
            .ToArray();

        return prioritized.Length == 0 ? HomeRowPriority.ToCharArray() : prioritized;
    }

    private static int ResolveFixedLabelLength(int candidateCount, int baseCount)
    {
        var length = 1;
        long capacity = baseCount;
        while (capacity < candidateCount)
        {
            length++;
            capacity *= baseCount;
        }

        return length;
    }

    private static string ToFixedLengthLabel(int zeroBasedIndex, char[] symbols, int length)
    {
        var baseCount = symbols.Length;
        var value = zeroBasedIndex;
        Span<char> buffer = stackalloc char[length];

        for (var position = 0; position < length; position++)
        {
            var remainder = value % baseCount;
            buffer[position] = symbols[remainder];
            value /= baseCount;
        }

        return new string(buffer);
    }
}
